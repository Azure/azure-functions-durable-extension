// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.AzureStorage;
using DurableTask.Core;
using DurableTask.Core.Common;
using DurableTask.Core.Exceptions;
using DurableTask.Core.History;
using DurableTask.Core.Middleware;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Configuration for the Durable Functions extension.
    /// </summary>
    [Extension("DurableTask", "DurableTask")]
    public class DurableTaskExtension :
        IExtensionConfigProvider,
        IAsyncConverter<HttpRequestMessage, HttpResponseMessage>,
        INameVersionObjectManager<TaskOrchestration>,
        INameVersionObjectManager<TaskActivity>
    {
        private static readonly string LoggerCategoryName = LogCategories.CreateTriggerCategory("DurableTask");

        // Creating client objects is expensive, so we cache them when the attributes match.
        // Note that OrchestrationClientAttribute defines a custom equality comparer.
        private readonly ConcurrentDictionary<OrchestrationClientAttribute, DurableOrchestrationClient> cachedClients =
            new ConcurrentDictionary<OrchestrationClientAttribute, DurableOrchestrationClient>();

        private readonly ConcurrentDictionary<FunctionName, RegisteredFunctionInfo> knownOrchestrators =
            new ConcurrentDictionary<FunctionName, RegisteredFunctionInfo>();

        private readonly ConcurrentDictionary<FunctionName, RegisteredFunctionInfo> knownEntities =
            new ConcurrentDictionary<FunctionName, RegisteredFunctionInfo>();

        private readonly ConcurrentDictionary<FunctionName, RegisteredFunctionInfo> knownActivities =
            new ConcurrentDictionary<FunctionName, RegisteredFunctionInfo>();

        private readonly AsyncLock taskHubLock = new AsyncLock();

        private readonly IOrchestrationServiceFactory orchestrationServiceFactory;
        private readonly INameResolver nameResolver;
        private IOrchestrationService orchestrationService;
        private TaskHubWorker taskHubWorker;
        private bool isTaskHubWorkerStarted;

        /// <summary>
        /// Initializes a new instance of the <see cref="DurableTaskExtension"/>.
        /// </summary>
        /// <param name="options">The configuration options for this extension.</param>
        /// <param name="loggerFactory">The logger factory used for extension-specific logging and orchestration tracking.</param>
        /// <param name="nameResolver">The name resolver to use for looking up application settings.</param>
        /// <param name="orchestrationServiceFactory">The factory used to create orchestration service based on the configured storage provider.</param>
        public DurableTaskExtension(
            IOptions<DurableTaskOptions> options,
            ILoggerFactory loggerFactory,
            INameResolver nameResolver,
            IOrchestrationServiceFactory orchestrationServiceFactory)
        {
            this.Options = options.Value;
            this.nameResolver = nameResolver ?? throw new ArgumentNullException(nameof(nameResolver));

            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            ILogger logger = loggerFactory.CreateLogger(LoggerCategoryName);

            this.TraceHelper = new EndToEndTraceHelper(logger, this.Options.Tracing.TraceReplayEvents);
            this.HttpApiHandler = new HttpApiHandler(this, logger);
            this.LifeCycleNotificationHelper = this.CreateLifeCycleNotificationHelper();
            this.orchestrationServiceFactory = orchestrationServiceFactory;
        }

        internal DurableTaskOptions Options { get; }

        internal HttpApiHandler HttpApiHandler { get; private set; }

        internal ILifeCycleNotificationHelper LifeCycleNotificationHelper { get; private set; }

        internal EndToEndTraceHelper TraceHelper { get; private set; }

        /// <summary>
        /// Internal initialization call from the WebJobs host.
        /// </summary>
        /// <param name="context">Extension context provided by WebJobs.</param>
        void IExtensionConfigProvider.Initialize(ExtensionConfigContext context)
        {
            if (this.nameResolver.TryResolveWholeString(this.Options.HubName, out string taskHubName))
            {
                // use the resolved task hub name
                this.Options.HubName = taskHubName;
            }

            // Throw if any of the configured options are invalid
            this.Options.Validate();

            // For 202 support
            if (this.Options.NotificationUrl == null)
            {
#pragma warning disable CS0618 // Type or member is obsolete
                this.Options.NotificationUrl = context.GetWebhookHandler();
#pragma warning restore CS0618 // Type or member is obsolete
            }

            this.TraceConfigurationSettings();

            var bindings = new BindingHelper(this, this.TraceHelper);

            // Note that the order of the rules is important
            var rule = context.AddBindingRule<OrchestrationClientAttribute>()
                .AddConverter<string, StartOrchestrationArgs>(bindings.StringToStartOrchestrationArgs)
                .AddConverter<JObject, StartOrchestrationArgs>(bindings.JObjectToStartOrchestrationArgs)
                .AddConverter<IDurableOrchestrationClient, string>(bindings.DurableOrchestrationClientToString);

            rule.BindToCollector<StartOrchestrationArgs>(bindings.CreateAsyncCollector);
            rule.BindToInput<IDurableOrchestrationClient>(this.GetClient);

            context.AddBindingRule<OrchestrationTriggerAttribute>()
                .BindToTrigger(new OrchestrationTriggerAttributeBindingProvider(this, context, this.TraceHelper));

            context.AddBindingRule<ActivityTriggerAttribute>()
                .BindToTrigger(new ActivityTriggerAttributeBindingProvider(this, context, this.TraceHelper));

            context.AddBindingRule<EntityTriggerAttribute>()
                .BindToTrigger(new EntityTriggerAttributeBindingProvider(this, context, this.TraceHelper));

            this.orchestrationService = this.orchestrationServiceFactory.GetOrchestrationService();

            this.taskHubWorker = new TaskHubWorker(this.orchestrationService, this, this);
            this.taskHubWorker.AddOrchestrationDispatcherMiddleware(this.EntityMiddleware);
            this.taskHubWorker.AddOrchestrationDispatcherMiddleware(this.OrchestrationMiddleware);
        }

        private void TraceConfigurationSettings()
        {
            this.TraceHelper.ExtensionInformationalEvent(
                this.Options.HubName,
                instanceId: string.Empty,
                functionName: string.Empty,
                message: $"Initializing extension with the following settings: {this.Options.GetDebugString()}",
                writeToUserLogs: true);
        }

        private ILifeCycleNotificationHelper CreateLifeCycleNotificationHelper()
        {
            // First: EventGrid
            if (this.Options.Notifications.EventGrid != null
                && (!string.IsNullOrEmpty(this.Options.Notifications.EventGrid.TopicEndpoint) || !string.IsNullOrEmpty(this.Options.Notifications.EventGrid.KeySettingName)))
            {
                return new EventGridLifeCycleNotificationHelper(this.Options, this.nameResolver, this.TraceHelper);
            }

            // Second: Custom LifeCycle Helper Type
            if (!string.IsNullOrEmpty(this.Options.CustomLifeCycleNotificationHelperType))
            {
                var notificationType = Type.GetType(this.Options.CustomLifeCycleNotificationHelperType);

                if (notificationType != null && typeof(ILifeCycleNotificationHelper).IsAssignableFrom(notificationType))
                {
                    return (ILifeCycleNotificationHelper)Activator.CreateInstance(notificationType);
                }
            }

            // Fallback: Disable Notification
            return new NullLifeCycleNotificationHelper();
        }

        /// <summary>
        /// Deletes all data stored in the current task hub.
        /// </summary>
        /// <returns>A task representing the async delete operation.</returns>
        public Task DeleteTaskHubAsync()
        {
            return this.orchestrationService.DeleteAsync();
        }

        /// <summary>
        /// Called by the Durable Task Framework: Not used.
        /// </summary>
        /// <param name="creator">This parameter is not used.</param>
        void INameVersionObjectManager<TaskOrchestration>.Add(ObjectCreator<TaskOrchestration> creator)
        {
            throw new InvalidOperationException("Orchestrations should never be added explicitly.");
        }

        /// <summary>
        /// Called by the Durable Task Framework: Returns the specified <see cref="TaskOrchestration"/>.
        /// </summary>
        /// <param name="name">The name of the orchestration to return.</param>
        /// <param name="version">Not used.</param>
        /// <returns>An orchestration shim that delegates execution to an orchestrator function.</returns>
        TaskOrchestration INameVersionObjectManager<TaskOrchestration>.GetObject(string name, string version)
        {
            if (name.StartsWith("@"))
            {
                return new TaskEntityShim(this, name);
            }
            else
            {
                return new TaskOrchestrationShim(this, name);
            }
        }

        /// <summary>
        /// Called by the durable task framework: Not used.
        /// </summary>
        /// <param name="creator">This parameter is not used.</param>
        void INameVersionObjectManager<TaskActivity>.Add(ObjectCreator<TaskActivity> creator)
        {
            throw new InvalidOperationException("Activities should never be added explicitly.");
        }

        /// <summary>
        /// Called by the Durable Task Framework: Returns the specified <see cref="TaskActivity"/>.
        /// </summary>
        /// <param name="name">The name of the activity to return.</param>
        /// <param name="version">Not used.</param>
        /// <returns>An activity shim that delegates execution to an activity function.</returns>
        TaskActivity INameVersionObjectManager<TaskActivity>.GetObject(string name, string version)
        {
            FunctionName activityFunction = new FunctionName(name);

            RegisteredFunctionInfo info;
            if (!this.knownActivities.TryGetValue(activityFunction, out info))
            {
                string message = $"Activity function '{activityFunction}' does not exist.";
                this.TraceHelper.ExtensionWarningEvent(
                    this.Options.HubName,
                    activityFunction.Name,
                    string.Empty /* TODO: Flow the instance id into this event */,
                    message);
                throw new InvalidOperationException(message);
            }

            return new TaskActivityShim(this, info.Executor, name);
        }

        private async Task OrchestrationMiddleware(DispatchMiddlewareContext dispatchContext, Func<Task> next)
        {
            TaskOrchestrationShim shim = dispatchContext.GetProperty<TaskOrchestration>() as TaskOrchestrationShim;
            if (shim == null)
            {
                // This is not an orchestration - skip.
                await next();
                return;
            }

            DurableCommonContext context = shim.Context;

            OrchestrationRuntimeState orchestrationRuntimeState = dispatchContext.GetProperty<OrchestrationRuntimeState>();

            if (orchestrationRuntimeState.ParentInstance != null)
            {
                context.ParentInstanceId = orchestrationRuntimeState.ParentInstance.OrchestrationInstance.InstanceId;
            }

            context.InstanceId = orchestrationRuntimeState.OrchestrationInstance.InstanceId;
            context.ExecutionId = orchestrationRuntimeState.OrchestrationInstance.ExecutionId;
            context.IsReplaying = orchestrationRuntimeState.ExecutionStartedEvent.IsPlayed;
            context.History = orchestrationRuntimeState.Events;
            context.RawInput = orchestrationRuntimeState.Input;

            var info = shim.GetFunctionInfo();
            if (info == null)
            {
                string message = this.GetInvalidOrchestratorFunctionMessage(context.FunctionName);
                this.TraceHelper.ExtensionWarningEvent(
                    this.Options.HubName,
                    orchestrationRuntimeState.Name,
                    orchestrationRuntimeState.OrchestrationInstance.InstanceId,
                    message);
                throw new InvalidOperationException(message);
            }

            // 1. Start the functions invocation pipeline (billing, logging, bindings, and timeout tracking).
            FunctionResult result = await info.Executor.TryExecuteAsync(
                new TriggeredFunctionData
                {
                    TriggerValue = context,

#pragma warning disable CS0618 // Approved for use by this extension
                    InvokeHandler = async userCodeInvoker =>
                    {
                        // 2. Configure the shim with the inner invoker to execute the user code.
                        shim.SetFunctionInvocationCallback(userCodeInvoker);

                        // 3. Move to the next stage of the DTFx pipeline to trigger the orchestrator shim.
                        await next();

                        // 4. If an activity failed, indicate to the functions Host that this execution failed via an exception
                        if (context.IsCompleted && context.OrchestrationException != null)
                        {
                            context.OrchestrationException.Throw();
                        }
                    },
#pragma warning restore CS0618
                },
                CancellationToken.None);

            if (!context.IsCompleted)
            {
                shim.TraceAwait();
            }

            if (context.IsCompleted &&
                context.PreserveUnprocessedEvents)
            {
                // Reschedule any unprocessed external events so that they can be picked up
                // in the next iteration.
                context.RescheduleBufferedExternalEvents();
            }

            await context.RunDeferredTasks();
        }

        private async Task EntityMiddleware(DispatchMiddlewareContext dispatchContext, Func<Task> next)
        {
            var entityShim = dispatchContext.GetProperty<TaskOrchestration>() as TaskEntityShim;
            if (entityShim == null)
            {
                // This is not an entity - skip.
                await next();
                return;
            }

            OrchestrationRuntimeState runtimeState = dispatchContext.GetProperty<OrchestrationRuntimeState>();
            DurableEntityContext entityContext = (DurableEntityContext)entityShim.Context;
            entityContext.InstanceId = runtimeState.OrchestrationInstance.InstanceId;
            entityContext.ExecutionId = runtimeState.OrchestrationInstance.ExecutionId;
            entityContext.History = runtimeState.Events;
            entityContext.RawInput = runtimeState.Input;

            // 1. Start the functions invocation pipeline (billing, logging, bindings, and timeout tracking).
            FunctionResult result = await entityShim.GetFunctionInfo().Executor.TryExecuteAsync(
                new TriggeredFunctionData
                {
                    TriggerValue = entityShim.Context,

#pragma warning disable CS0618 // Approved for use by this extension
                    InvokeHandler = async userCodeInvoker =>
                    {
                        entityShim.SetFunctionInvocationCallback(userCodeInvoker);

                        try
                        {
                            // 2. Load all the external events into the entity shim
                            foreach (HistoryEvent e in runtimeState.Events)
                            {
                                switch (e.EventType)
                                {
                                    case EventType.OrchestratorStarted:
                                        entityContext.CurrentOperationStartTime = e.Timestamp;
                                        break;
                                    case EventType.ExecutionStarted:
                                        entityShim.Rehydrate(runtimeState.Input);
                                        break;
                                    case EventType.EventRaised:
                                        EventRaisedEvent eventRaisedEvent = (EventRaisedEvent)e;
                                        this.TraceHelper.DeliveringEntityMessage(
                                            entityContext.InstanceId,
                                            entityContext.ExecutionId,
                                            e.EventId,
                                            eventRaisedEvent.Name,
                                            eventRaisedEvent.Input);
                                        entityShim.ProcessMessage(eventRaisedEvent.Name, eventRaisedEvent.Input);
                                        break;
                                }
                            }

                            // 3. Wait for all the queued entity operations to complete
                            await entityShim.WaitForLastOperation();

                            // 4. Run the scheduler orchestration so that state can be persisted.
                            await next();
                        }
                        catch (Exception e)
                        {
                            // something went wrong in our code (application exceptions don't make it here)

                            string exceptionDetails = e.ToString();

                            if (!entityContext.IsReplaying)
                            {
                                entityContext.AddDeferredTask(() => this.LifeCycleNotificationHelper.OrchestratorFailedAsync(
                                    entityContext.HubName,
                                    entityContext.Name,
                                    entityContext.InstanceId,
                                    exceptionDetails,
                                    entityContext.IsReplaying));
                            }

                            var entitySchedulerExceptions = new OrchestrationFailureException(
                                $"Entity scheduler {entityShim.EntityId} failed: {e.Message}",
                                Utils.SerializeCause(e, MessagePayloadDataConverter.ErrorConverter));

                            entityContext.OrchestrationException = ExceptionDispatchInfo.Capture(entitySchedulerExceptions);

                            throw entitySchedulerExceptions;
                        }
                    },
#pragma warning restore CS0618
                },
                CancellationToken.None);

            await entityContext.RunDeferredTasks();
        }

        internal RegisteredFunctionInfo GetOrchestratorInfo(FunctionName orchestratorFunction)
        {
            this.knownOrchestrators.TryGetValue(orchestratorFunction, out var info);
            return info;
        }

        internal RegisteredFunctionInfo GetEntityInfo(FunctionName entityFunction)
        {
            this.knownEntities.TryGetValue(entityFunction, out var info);
            return info;
        }

        /// <summary>
        /// Gets a <see cref="DurableOrchestrationClient"/> using configuration from a <see cref="OrchestrationClientAttribute"/> instance.
        /// </summary>
        /// <param name="attribute">The attribute containing the client configuration parameters.</param>
        /// <returns>Returns a <see cref="DurableOrchestrationClient"/> instance. The returned instance may be a cached instance.</returns>
        protected internal virtual IDurableOrchestrationClient GetClient(OrchestrationClientAttribute attribute)
        {
            DurableOrchestrationClient client = this.cachedClients.GetOrAdd(
                attribute,
                attr =>
                {
                    IOrchestrationServiceClient innerClient = this.orchestrationServiceFactory.GetOrchestrationClient(attribute);
                    return new DurableOrchestrationClient(innerClient, this, this.HttpApiHandler, attr);
                });

            return client;
        }

        internal void RegisterOrchestrator(FunctionName orchestratorFunction, RegisteredFunctionInfo orchestratorInfo)
        {
            if (orchestratorInfo != null)
            {
                orchestratorInfo.IsDeregistered = false;
            }

            if (this.knownOrchestrators.TryAdd(orchestratorFunction, orchestratorInfo))
            {
                this.TraceHelper.ExtensionInformationalEvent(
                    this.Options.HubName,
                    instanceId: string.Empty,
                    functionName: orchestratorFunction.Name,
                    message: $"Registered orchestrator function named {orchestratorFunction}.",
                    writeToUserLogs: false);
            }
            else
            {
                this.knownOrchestrators[orchestratorFunction] = orchestratorInfo;
            }
        }

        internal void DeregisterOrchestrator(FunctionName orchestratorFunction)
        {
            RegisteredFunctionInfo existing;
            if (this.knownOrchestrators.TryGetValue(orchestratorFunction, out existing) && !existing.IsDeregistered)
            {
                existing.IsDeregistered = true;

                this.TraceHelper.ExtensionInformationalEvent(
                    this.Options.HubName,
                    instanceId: string.Empty,
                    functionName: orchestratorFunction.Name,
                    message: $"Deregistered orchestrator function named {orchestratorFunction}.",
                    writeToUserLogs: false);
            }
        }

        internal void RegisterActivity(FunctionName activityFunction, ITriggeredFunctionExecutor executor)
        {
            if (this.knownActivities.TryGetValue(activityFunction, out RegisteredFunctionInfo existing))
            {
                existing.Executor = executor;
            }
            else
            {
                this.TraceHelper.ExtensionInformationalEvent(
                    this.Options.HubName,
                    instanceId: string.Empty,
                    functionName: activityFunction.Name,
                    message: $"Registering activity function named {activityFunction}.",
                    writeToUserLogs: false);

                var info = new RegisteredFunctionInfo(executor, isOutOfProc: false);
                this.knownActivities[activityFunction] = info;
            }
        }

        internal void DeregisterActivity(FunctionName activityFunction)
        {
            RegisteredFunctionInfo info;
            if (this.knownActivities.TryGetValue(activityFunction, out info) && !info.IsDeregistered)
            {
                info.IsDeregistered = true;

                this.TraceHelper.ExtensionInformationalEvent(
                    this.Options.HubName,
                    instanceId: string.Empty,
                    functionName: activityFunction.Name,
                    message: $"Deregistered activity function named {activityFunction}.",
                    writeToUserLogs: false);
            }
        }

        internal void RegisterEntity(FunctionName entityFunction, RegisteredFunctionInfo entityInfo)
        {
            if (entityInfo != null)
            {
                entityInfo.IsDeregistered = false;
            }

            if (this.knownEntities.TryAdd(entityFunction, entityInfo))
            {
                this.TraceHelper.ExtensionInformationalEvent(
                    this.Options.HubName,
                    instanceId: string.Empty,
                    functionName: entityFunction.Name,
                    message: $"Registered entity function named {entityFunction}.",
                    writeToUserLogs: false);
            }
            else
            {
                this.knownEntities[entityFunction] = entityInfo;
            }
        }

        internal void DeregisterEntity(FunctionName entityFunction)
        {
            RegisteredFunctionInfo existing;
            if (this.knownOrchestrators.TryGetValue(entityFunction, out existing) && !existing.IsDeregistered)
            {
                existing.IsDeregistered = true;

                this.TraceHelper.ExtensionInformationalEvent(
                    this.Options.HubName,
                    instanceId: string.Empty,
                    functionName: entityFunction.Name,
                    message: $"Deregistered entity function named {entityFunction}.",
                    writeToUserLogs: false);
            }
        }

        internal void ThrowIfFunctionDoesNotExist(string name, FunctionType functionType)
        {
            var functionName = new FunctionName(name);

            if (functionType == FunctionType.Activity && !this.knownActivities.ContainsKey(functionName))
            {
                throw new ArgumentException(this.GetInvalidActivityFunctionMessage(name));
            }
            else if (functionType == FunctionType.Orchestrator && !this.knownOrchestrators.ContainsKey(functionName))
            {
                throw new ArgumentException(this.GetInvalidOrchestratorFunctionMessage(name));
            }
        }

        internal string GetInvalidActivityFunctionMessage(string name)
        {
            string message = $"The function '{name}' doesn't exist, is disabled, or is not an activity function. Additional info: ";
            if (this.knownActivities.Keys.Count > 0)
            {
                message += $"The following are the known activity functions: '{string.Join("', '", this.knownActivities.Keys)}'.";
            }
            else
            {
                message += "No activity functions are currently registered!";
            }

            return message;
        }

        internal string GetInvalidOrchestratorFunctionMessage(string name)
        {
            string message = $"The function '{name}' doesn't exist, is disabled, or is not an orchestrator function. Additional info: ";
            if (this.knownOrchestrators.Keys.Count > 0)
            {
                message += $"The following are the known orchestrator functions: '{string.Join("', '", this.knownOrchestrators.Keys)}'.";
            }
            else
            {
                message += "No orchestrator functions are currently registered!";
            }

            return message;
        }

        internal async Task<bool> StartTaskHubWorkerIfNotStartedAsync()
        {
            if (!this.isTaskHubWorkerStarted)
            {
                using (await this.taskHubLock.AcquireAsync())
                {
                    if (!this.isTaskHubWorkerStarted)
                    {
                        this.TraceHelper.ExtensionInformationalEvent(
                            this.Options.HubName,
                            instanceId: string.Empty,
                            functionName: string.Empty,
                            message: "Starting task hub worker",
                            writeToUserLogs: true);

                        await this.orchestrationService.CreateIfNotExistsAsync();
                        await this.taskHubWorker.StartAsync();

                        // Enable flowing exception information from activities
                        // to the parent orchestration code.
                        this.taskHubWorker.TaskActivityDispatcher.IncludeDetails = true;
                        this.taskHubWorker.TaskOrchestrationDispatcher.IncludeDetails = true;
                        this.isTaskHubWorkerStarted = true;
                        return true;
                    }
                }
            }

            return false;
        }

        internal async Task<bool> StopTaskHubWorkerIfIdleAsync()
        {
            using (await this.taskHubLock.AcquireAsync())
            {
                if (this.isTaskHubWorkerStarted &&
                    this.knownOrchestrators.Values.Count(info => !info.IsDeregistered) == 0 &&
                    this.knownActivities.Values.Count(info => !info.IsDeregistered) == 0)
                {
                    this.TraceHelper.ExtensionInformationalEvent(
                        this.Options.HubName,
                        instanceId: string.Empty,
                        functionName: string.Empty,
                        message: "Stopping task hub worker",
                        writeToUserLogs: true);

                    await this.taskHubWorker.StopAsync(isForced: true);
                    this.isTaskHubWorkerStarted = false;
                    return true;
                }
            }

            return false;
        }

        internal string GetIntputOutputTrace(string rawInputOutputData)
        {
            if (this.Options.Tracing.TraceInputsAndOutputs)
            {
                return rawInputOutputData;
            }
            else if (rawInputOutputData == null)
            {
                return "(null)";
            }
            else
            {
                // Azure Storage uses UTF-32 encoding for string payloads
                return "(" + Encoding.UTF32.GetByteCount(rawInputOutputData) + " bytes)";
            }
        }

        /// <inheritdoc/>
        Task<HttpResponseMessage> IAsyncConverter<HttpRequestMessage, HttpResponseMessage>.ConvertAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return this.HttpApiHandler.HandleRequestAsync(request);
        }

        internal static string ValidatePayloadSize(string payload)
        {
            // The payload gets written to Azure Table Storage and to Azure Queues, which have
            // strict storage limitations (64 KB). Until we support large messages, we need to block them.
            // https://github.com/Azure/azure-functions-durable-extension/issues/79
            // We limit to 60 KB (UTF-32 encoding) to leave room for metadata.
            if (Encoding.UTF32.GetByteCount(payload) > 60 * 1024)
            {
                throw new ArgumentException("The size of the JSON-serialized payload must not exceed 60 KB of UTF-32 encoded text.");
            }

            return payload;
        }
    }
}
