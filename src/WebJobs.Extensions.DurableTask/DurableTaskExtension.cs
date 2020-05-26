// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.AzureStorage;
using DurableTask.Core;
using DurableTask.Core.Exceptions;
using DurableTask.Core.History;
using DurableTask.Core.Middleware;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Listener;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Configuration for the Durable Functions extension.
    /// </summary>
#if !FUNCTIONS_V1
    [Extension("DurableTask", "DurableTask")]
#endif
    public class DurableTaskExtension :
        IExtensionConfigProvider,
        IDisposable,
        IAsyncConverter<HttpRequestMessage, HttpResponseMessage>,
        INameVersionObjectManager<TaskOrchestration>,
        INameVersionObjectManager<TaskActivity>
    {
        private static readonly string LoggerCategoryName = LogCategories.CreateTriggerCategory("DurableTask");

        // Creating client objects is expensive, so we cache them when the attributes match.
        // Note that DurableClientAttribute defines a custom equality comparer.
        private readonly ConcurrentDictionary<DurableClientAttribute, DurableClient> cachedClients =
            new ConcurrentDictionary<DurableClientAttribute, DurableClient>();

        private readonly ConcurrentDictionary<FunctionName, RegisteredFunctionInfo> knownOrchestrators =
            new ConcurrentDictionary<FunctionName, RegisteredFunctionInfo>();

        private readonly ConcurrentDictionary<FunctionName, RegisteredFunctionInfo> knownEntities =
            new ConcurrentDictionary<FunctionName, RegisteredFunctionInfo>();

        private readonly ConcurrentDictionary<FunctionName, RegisteredFunctionInfo> knownActivities =
            new ConcurrentDictionary<FunctionName, RegisteredFunctionInfo>();

        private readonly AsyncLock taskHubLock = new AsyncLock();

        private readonly bool isOptionsConfigured;
        private readonly IApplicationLifetimeWrapper hostLifetimeService = HostLifecycleService.NoOp;

        private IDurabilityProviderFactory durabilityProviderFactory;
        private INameResolver nameResolver;
        private DurabilityProvider defaultDurabilityProvider;
        private TaskHubWorker taskHubWorker;
        private bool isTaskHubWorkerStarted;
        private HttpClient durableHttpClient;

#if FUNCTIONS_V1
        private IConnectionStringResolver connectionStringResolver;

        /// <summary>
        /// Obsolete. Please use an alternate constructor overload.
        /// </summary>
        [Obsolete("The default constructor is obsolete and will be removed in future versions")]
        public DurableTaskExtension()
        {
            // Options initialization happens later
            this.Options = new DurableTaskOptions();
            this.isOptionsConfigured = false;
        }
#endif

        /// <summary>
        /// Initializes a new instance of the <see cref="DurableTaskExtension"/>.
        /// </summary>
        /// <param name="options">The configuration options for this extension.</param>
        /// <param name="loggerFactory">The logger factory used for extension-specific logging and orchestration tracking.</param>
        /// <param name="nameResolver">The name resolver to use for looking up application settings.</param>
        /// <param name="orchestrationServiceFactory">The factory used to create orchestration service based on the configured storage provider.</param>
        /// <param name="durableHttpMessageHandlerFactory">The HTTP message handler that handles HTTP requests and HTTP responses.</param>
        /// <param name="hostLifetimeService">The host shutdown notification service for detecting and reacting to host shutdowns.</param>
        /// <param name="lifeCycleNotificationHelper">The lifecycle notification helper used for custom orchestration tracking.</param>
        /// <param name="messageSerializerSettingsFactory">The factory used to create <see cref="JsonSerializerSettings"/> for message settings.</param>
        /// <param name="errorSerializerSettingsFactory">The factory used to create <see cref="JsonSerializerSettings"/> for error settings.</param>
        public DurableTaskExtension(
            IOptions<DurableTaskOptions> options,
            ILoggerFactory loggerFactory,
            INameResolver nameResolver,
            IDurabilityProviderFactory orchestrationServiceFactory,
            IApplicationLifetimeWrapper hostLifetimeService,
            IDurableHttpMessageHandlerFactory durableHttpMessageHandlerFactory = null,
            ILifeCycleNotificationHelper lifeCycleNotificationHelper = null,
            IMessageSerializerSettingsFactory messageSerializerSettingsFactory = null,
            IErrorSerializerSettingsFactory errorSerializerSettingsFactory = null)
        {
            // Options will be null in Functions v1 runtime - populated later.
            this.Options = options?.Value ?? new DurableTaskOptions();
            this.nameResolver = nameResolver ?? throw new ArgumentNullException(nameof(nameResolver));
            this.ResolveAppSettingOptions();

            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            ILogger logger = loggerFactory.CreateLogger(LoggerCategoryName);

            this.TraceHelper = new EndToEndTraceHelper(logger, this.Options.Tracing.TraceReplayEvents);
            this.LifeCycleNotificationHelper = lifeCycleNotificationHelper ?? this.CreateLifeCycleNotificationHelper();
            this.durabilityProviderFactory = orchestrationServiceFactory;
            this.defaultDurabilityProvider = this.durabilityProviderFactory.GetDurabilityProvider();
            this.isOptionsConfigured = true;

            if (durableHttpMessageHandlerFactory == null)
            {
                durableHttpMessageHandlerFactory = new DurableHttpMessageHandlerFactory();
            }

            DurableHttpClientFactory durableHttpClientFactory = new DurableHttpClientFactory();
            this.durableHttpClient = durableHttpClientFactory.GetClient(durableHttpMessageHandlerFactory);

            this.MessageDataConverter = this.CreateMessageDataConverter(messageSerializerSettingsFactory);
            this.ErrorDataConverter = this.CreateErrorDataConverter(errorSerializerSettingsFactory);

            this.HttpApiHandler = new HttpApiHandler(this, logger);

            this.hostLifetimeService = hostLifetimeService;

#if !FUNCTIONS_V1
            // The RPC server is started when the extension is initialized.
            // The RPC server is stopped when the host has finished shutting down.
            this.hostLifetimeService.OnStopped.Register(this.StopLocalRcpServer);
#endif
        }

#if FUNCTIONS_V1
        internal DurableTaskExtension(
            IOptions<DurableTaskOptions> options,
            ILoggerFactory loggerFactory,
            INameResolver nameResolver,
            IDurabilityProviderFactory orchestrationServiceFactory,
            IConnectionStringResolver connectionStringResolver,
            IApplicationLifetimeWrapper shutdownNotification,
            IDurableHttpMessageHandlerFactory durableHttpMessageHandlerFactory)
            : this(options, loggerFactory, nameResolver, orchestrationServiceFactory, shutdownNotification, durableHttpMessageHandlerFactory)
        {
            this.connectionStringResolver = connectionStringResolver;
        }

        /// <summary>
        /// Gets or sets default task hub name to be used by all <see cref="IDurableClient"/>, <see cref="IDurableOrchestrationClient"/>, <see cref="IDurableEntityClient"/>,
        /// <see cref="IDurableOrchestrationContext"/>, and <see cref="IDurableActivityContext"/> instances.
        /// </summary>
        /// <remarks>
        /// A task hub is a logical grouping of storage resources. Alternate task hub names can be used to isolate
        /// multiple Durable Functions applications from each other, even if they are using the same storage backend.
        /// </remarks>
        /// <value>The name of the default task hub.</value>
        public string HubName
        {
            get { return this.Options.HubName; }
            set { this.Options.HubName = value; }
        }
#endif

        internal DurableTaskOptions Options { get; }

        internal HttpApiHandler HttpApiHandler { get; private set; }

        internal ILifeCycleNotificationHelper LifeCycleNotificationHelper { get; private set; }

        internal EndToEndTraceHelper TraceHelper { get; private set; }

        internal MessagePayloadDataConverter MessageDataConverter { get; private set; }

        internal MessagePayloadDataConverter ErrorDataConverter { get; private set; }

        private MessagePayloadDataConverter CreateMessageDataConverter(IMessageSerializerSettingsFactory messageSerializerSettingsFactory)
        {
            bool isDefault;
            if (messageSerializerSettingsFactory == null)
            {
                messageSerializerSettingsFactory = new MessageSerializerSettingsFactory();
            }

            isDefault = messageSerializerSettingsFactory is MessageSerializerSettingsFactory;

            return new MessagePayloadDataConverter(messageSerializerSettingsFactory.CreateJsonSerializerSettings(), isDefault);
        }

        private MessagePayloadDataConverter CreateErrorDataConverter(IErrorSerializerSettingsFactory errorSerializerSettingsFactory)
        {
            bool isDefault;
            if (errorSerializerSettingsFactory == null)
            {
                errorSerializerSettingsFactory = new ErrorSerializerSettingsFactory();
            }

            isDefault = errorSerializerSettingsFactory is ErrorSerializerSettingsFactory;

            return new MessagePayloadDataConverter(errorSerializerSettingsFactory.CreateJsonSerializerSettings(), isDefault);
        }

        /// <summary>
        /// Internal initialization call from the WebJobs host.
        /// </summary>
        /// <param name="context">Extension context provided by WebJobs.</param>
        void IExtensionConfigProvider.Initialize(ExtensionConfigContext context)
        {
            ConfigureLoaderHooks();

            // Functions V1 has it's configuration initialized at startup time (now).
            // For Functions V2 (and for some unit tests) configuration happens earlier in the pipeline.
            if (!this.isOptionsConfigured)
            {
                this.InitializeForFunctionsV1(context);
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
            var rule = context.AddBindingRule<DurableClientAttribute>()
                .AddConverter<string, StartOrchestrationArgs>(bindings.StringToStartOrchestrationArgs)
                .AddConverter<JObject, StartOrchestrationArgs>(bindings.JObjectToStartOrchestrationArgs)
                .AddConverter<IDurableClient, string>(bindings.DurableOrchestrationClientToString);

            rule.BindToCollector<StartOrchestrationArgs>(bindings.CreateAsyncCollector);
            rule.BindToInput<IDurableOrchestrationClient>(this.GetClient);
            rule.BindToInput<IDurableEntityClient>(this.GetClient);
            rule.BindToInput<IDurableClient>(this.GetClient);

            // We add a binding rule to support the now deprecated `orchestrationClient` binding
            // A cleaner solution would be to have the prior rule have an OR-style selector between
            // DurableClientAttribute and OrchestrationClientAttribute, but it's unclear if that's
            // possible (for now).
#pragma warning disable CS0618 // Type or member is obsolete
            var backwardsCompRule = context.AddBindingRule<OrchestrationClientAttribute>()
#pragma warning restore CS0618 // Type or member is obsolete
                .AddConverter<string, StartOrchestrationArgs>(bindings.StringToStartOrchestrationArgs)
                .AddConverter<JObject, StartOrchestrationArgs>(bindings.JObjectToStartOrchestrationArgs)
                .AddConverter<IDurableClient, string>(bindings.DurableOrchestrationClientToString);

            backwardsCompRule.BindToCollector<StartOrchestrationArgs>(bindings.CreateAsyncCollector);
            backwardsCompRule.BindToInput<IDurableOrchestrationClient>(this.GetClient);
            backwardsCompRule.BindToInput<IDurableEntityClient>(this.GetClient);
            backwardsCompRule.BindToInput<IDurableClient>(this.GetClient);

            string storageConnectionString = null;
            var providerFactory = this.durabilityProviderFactory as AzureStorageDurabilityProviderFactory;
            if (providerFactory != null)
            {
                storageConnectionString = providerFactory.GetDefaultStorageConnectionString();
            }

            context.AddBindingRule<OrchestrationTriggerAttribute>()
                .BindToTrigger(new OrchestrationTriggerAttributeBindingProvider(this, context, storageConnectionString, this.TraceHelper));

            context.AddBindingRule<ActivityTriggerAttribute>()
                .BindToTrigger(new ActivityTriggerAttributeBindingProvider(this, context, storageConnectionString, this.TraceHelper));

            context.AddBindingRule<EntityTriggerAttribute>()
                .BindToTrigger(new EntityTriggerAttributeBindingProvider(this, context, storageConnectionString, this.TraceHelper));

            this.taskHubWorker = new TaskHubWorker(this.defaultDurabilityProvider, this, this);
            this.taskHubWorker.AddOrchestrationDispatcherMiddleware(this.EntityMiddleware);
            this.taskHubWorker.AddOrchestrationDispatcherMiddleware(this.OrchestrationMiddleware);

#if !FUNCTIONS_V1
            // The RPC server needs to be started sometime before any functions can be triggered
            // and this is the latest point in the pipeline available to us.
            this.StartLocalRcpServer();
#endif
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.HttpApiHandler?.Dispose();
        }

#if !FUNCTIONS_V1
        private void StartLocalRcpServer()
        {
            bool? shouldEnable = this.Options.LocalRpcEndpointEnabled;
            if (!shouldEnable.HasValue)
            {
                // Default behavior is to only start the local RPC server for non-.NET function languages.
                // We'll enable it if it's non-.NET or if the FUNCTIONS_WORKER_RUNTIME value isn't present.
                string functionsWorkerRuntime = this.nameResolver.Resolve("FUNCTIONS_WORKER_RUNTIME");
                shouldEnable = !string.Equals(functionsWorkerRuntime, "dotnet", StringComparison.OrdinalIgnoreCase);
            }

            if (shouldEnable == true)
            {
                this.HttpApiHandler.StartLocalHttpServerAsync().GetAwaiter().GetResult();
            }
        }

        private void StopLocalRcpServer()
        {
            this.HttpApiHandler.StopLocalHttpServerAsync().GetAwaiter().GetResult();
        }
#endif

        private void ResolveAppSettingOptions()
        {
            if (this.Options == null)
            {
                throw new InvalidOperationException($"{nameof(this.Options)} must be set before resolving app settings.");
            }

            if (this.nameResolver == null)
            {
                throw new InvalidOperationException($"{nameof(this.nameResolver)} must be set before resolving app settings.");
            }

            if (this.nameResolver.TryResolveWholeString(this.Options.HubName, out string taskHubName))
            {
                // use the resolved task hub name
                this.Options.HubName = taskHubName;
            }
        }

        private void InitializeForFunctionsV1(ExtensionConfigContext context)
        {
#if FUNCTIONS_V1
            context.ApplyConfig(this.Options, "DurableTask");
            this.nameResolver = context.Config.NameResolver;
            this.ResolveAppSettingOptions();
            ILogger logger = context.Config.LoggerFactory.CreateLogger(LoggerCategoryName);
            this.TraceHelper = new EndToEndTraceHelper(logger, this.Options.Tracing.TraceReplayEvents);
            this.connectionStringResolver = new WebJobsConnectionStringProvider();
            this.durabilityProviderFactory = new AzureStorageDurabilityProviderFactory(new OptionsWrapper<DurableTaskOptions>(this.Options), this.connectionStringResolver, this.nameResolver);
            this.defaultDurabilityProvider = this.durabilityProviderFactory.GetDurabilityProvider();
            this.LifeCycleNotificationHelper = this.CreateLifeCycleNotificationHelper();
            var messageSerializerSettingsFactory = new MessageSerializerSettingsFactory();
            var errorSerializerSettingsFactory = new ErrorSerializerSettingsFactory();
            this.MessageDataConverter = new MessagePayloadDataConverter(messageSerializerSettingsFactory.CreateJsonSerializerSettings(), true);
            this.ErrorDataConverter = new MessagePayloadDataConverter(errorSerializerSettingsFactory.CreateJsonSerializerSettings(), true);
            this.HttpApiHandler = new HttpApiHandler(this, logger);
#endif
        }

        private void TraceConfigurationSettings()
        {
            this.Options.TraceConfiguration(
                this.TraceHelper,
                this.defaultDurabilityProvider.ConfigurationJson);
        }

        private ILifeCycleNotificationHelper CreateLifeCycleNotificationHelper()
        {
            // First: EventGrid
            if (this.Options.Notifications.EventGrid != null
                && (!string.IsNullOrEmpty(this.Options.Notifications.EventGrid.TopicEndpoint) || !string.IsNullOrEmpty(this.Options.Notifications.EventGrid.KeySettingName)))
            {
                return new EventGridLifeCycleNotificationHelper(this.Options, this.nameResolver, this.TraceHelper);
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
            return this.defaultDurabilityProvider.DeleteAsync();
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
                return new TaskOrchestrationShim(this, this.defaultDurabilityProvider, name);
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
            if (IsDurableHttpTask(name))
            {
                return new TaskHttpActivityShim(this, this.durableHttpClient);
            }

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

            return new TaskActivityShim(this, info.Executor, this.hostLifetimeService, name);
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

            DurableOrchestrationContext context = (DurableOrchestrationContext)shim.Context;

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
            WrappedFunctionResult result = await FunctionExecutionHelper.ExecuteFunctionInOrchestrationMiddleware(
                info.Executor,
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
                this.hostLifetimeService.OnStopping);

            if (result.ExecutionStatus == WrappedFunctionResult.FunctionResultStatus.FunctionsRuntimeError)
            {
                this.TraceHelper.FunctionAborted(
                    this.Options.HubName,
                    context.FunctionName,
                    context.InstanceId,
                    $"An internal error occurred while attempting to execute this function. The execution will be aborted and retried. Details: {result.Exception}",
                    functionType: FunctionType.Orchestrator);

                // This will abort the execution and cause the message to go back onto the queue for re-processing
                throw new SessionAbortedException(
                    $"An internal error occurred while attempting to execute '{context.FunctionName}'.", result.Exception);
            }

            if (!context.IsCompleted)
            {
                this.TraceHelper.FunctionAwaited(
                    context.HubName,
                    context.Name,
                    FunctionType.Orchestrator,
                    context.InstanceId,
                    context.IsReplaying);
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

            try
            {
                // 1. First time through the history
                // we count events, add any under-lock op to the batch, and process lock releases
                foreach (HistoryEvent e in runtimeState.Events)
                {
                    switch (e.EventType)
                    {
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

                            entityShim.NumberEventsToReceive++;

                            if (EntityMessageEventNames.IsRequestMessage(eventRaisedEvent.Name))
                            {
                                // we are receiving an operation request or a lock request
                                var requestMessage = this.MessageDataConverter.Deserialize<RequestMessage>(eventRaisedEvent.Input);

                                IEnumerable<RequestMessage> deliverNow;

                                if (requestMessage.ScheduledTime.HasValue)
                                {
                                    // messages with a scheduled time are always delivered immediately
                                    deliverNow = new RequestMessage[] { requestMessage };
                                }
                                else
                                {
                                    // run this through the message sorter to help with reordering and duplicate filtering
                                    deliverNow = entityContext.State.MessageSorter.ReceiveInOrder(requestMessage, entityContext.EntityMessageReorderWindow);
                                }

                                foreach (var message in deliverNow)
                                {
                                    if (entityContext.State.LockedBy == message.ParentInstanceId)
                                    {
                                        // operation requests from the lock holder are processed immediately
                                        entityShim.AddOperationToBatch(message);
                                    }
                                    else
                                    {
                                        // others go to the back of the queue
                                        entityContext.State.Enqueue(message);
                                    }
                                }
                            }
                            else
                            {
                                // we are receiving a lock release
                                var message = this.MessageDataConverter.Deserialize<ReleaseMessage>(eventRaisedEvent.Input);

                                if (entityContext.State.LockedBy == message.ParentInstanceId)
                                {
                                    this.TraceHelper.EntityLockReleased(
                                        entityContext.HubName,
                                        entityContext.Name,
                                        entityContext.InstanceId,
                                        message.ParentInstanceId,
                                        message.LockRequestId,
                                        isReplay: false);

                                    entityContext.State.LockedBy = null;
                                }
                            }

                            break;
                    }
                }

                // 2. We add as many requests from the queue to the batch as possible (stopping at lock requests)
                while (entityContext.State.LockedBy == null
                    && entityContext.State.TryDequeue(out var request))
                {
                    if (request.IsLockRequest)
                    {
                        entityShim.AddLockRequestToBatch(request);
                        entityContext.State.LockedBy = request.ParentInstanceId;
                    }
                    else
                    {
                        entityShim.AddOperationToBatch(request);
                    }
                }
            }
            catch (Exception e)
            {
                entityContext.CaptureInternalError(e);
            }

            // 3. Start the functions invocation pipeline (billing, logging, bindings, and timeout tracking).
            WrappedFunctionResult result = await FunctionExecutionHelper.ExecuteFunctionInOrchestrationMiddleware(
                entityShim.GetFunctionInfo().Executor,
                new TriggeredFunctionData
                {
                    TriggerValue = entityShim.Context,
#pragma warning disable CS0618 // Approved for use by this extension
                    InvokeHandler = async userCodeInvoker =>
                    {
                        entityShim.SetFunctionInvocationCallback(userCodeInvoker);

                        // 3. Run all the operations in the batch
                        if (entityContext.InternalError == null)
                        {
                            try
                            {
                                await entityShim.ExecuteBatch();
                            }
                            catch (Exception e)
                            {
                                entityContext.CaptureInternalError(e);
                            }
                        }

                        // 4. Run the DTFx orchestration to persist the effects,
                        // send the outbox, and continue as new
                        await next();

                        // 5. If there were internal or application errors, indicate to the functions host
                        entityContext.ThrowInternalExceptionIfAny();
                        entityContext.ThrowApplicationExceptionsIfAny();
                    },
#pragma warning restore CS0618
                },
                this.hostLifetimeService.OnStopping);

            if (result.ExecutionStatus == WrappedFunctionResult.FunctionResultStatus.FunctionsRuntimeError)
            {
                this.TraceHelper.FunctionAborted(
                    this.Options.HubName,
                    entityContext.FunctionName,
                    entityContext.InstanceId,
                    $"An internal error occurred while attempting to execute this function. The execution will be aborted and retried. Details: {result.Exception}",
                    functionType: FunctionType.Orchestrator);

                // This will abort the execution and cause the message to go back onto the queue for re-processing
                throw new SessionAbortedException(
                    $"An internal error occurred while attempting to execute '{entityContext.FunctionName}'.", result.Exception);
            }

            await entityContext.RunDeferredTasks();

            // If there were internal errors, do not commit the batch, but instead rethrow
            // here so DTFx can abort the batch and back off the work item
            entityContext.ThrowInternalExceptionIfAny();
        }

        internal string GetDefaultConnectionName()
        {
            return this.defaultDurabilityProvider.ConnectionName;
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

        // This is temporary until script loading
        private static void ConfigureLoaderHooks()
        {
#if FUNCTIONS_V1
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
#endif
        }

        private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            if (args.Name.StartsWith("DurableTask.Core"))
            {
                return typeof(TaskOrchestration).Assembly;
            }
            else if (args.Name.StartsWith("DurableTask.AzureStorage"))
            {
                return typeof(AzureStorageOrchestrationService).Assembly;
            }
            else if (args.Name.StartsWith("Microsoft.Azure.WebJobs.DurableTask"))
            {
                return typeof(DurableTaskExtension).Assembly;
            }

            return null;
        }

        /// <summary>
        /// Gets a <see cref="IDurableClient"/> using configuration from a <see cref="DurableClientAttribute"/> instance.
        /// </summary>
        /// <param name="attribute">The attribute containing the client configuration parameters.</param>
        /// <returns>Returns a <see cref="IDurableClient"/> instance. The returned instance may be a cached instance.</returns>
        protected internal virtual IDurableClient GetClient(DurableClientAttribute attribute)
        {
            DurableClient client = this.cachedClients.GetOrAdd(
                attribute,
                attr =>
                {
                    DurabilityProvider innerClient = this.durabilityProviderFactory.GetDurabilityProvider(attribute);
                    return new DurableClient(innerClient, this, this.HttpApiHandler, attr);
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
                var info = new RegisteredFunctionInfo(executor, isOutOfProc: false);
                this.knownActivities[activityFunction] = info;

                this.TraceHelper.ExtensionInformationalEvent(
                    this.Options.HubName,
                    instanceId: string.Empty,
                    functionName: activityFunction.Name,
                    message: $"Registered activity function named {activityFunction}.",
                    writeToUserLogs: false);
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
            if (this.knownEntities.TryGetValue(entityFunction, out existing) && !existing.IsDeregistered)
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
            if (IsDurableHttpTask(name))
            {
                return;
            }

            var functionName = new FunctionName(name);

            if (functionType == FunctionType.Activity && !this.knownActivities.ContainsKey(functionName))
            {
                throw new ArgumentException(this.GetInvalidActivityFunctionMessage(name));
            }
            else if (functionType == FunctionType.Orchestrator && !this.knownOrchestrators.ContainsKey(functionName))
            {
                throw new ArgumentException(this.GetInvalidOrchestratorFunctionMessage(name));
            }
            else if (functionType == FunctionType.Entity && !this.knownEntities.ContainsKey(functionName))
            {
                throw new ArgumentException(this.GetInvalidEntityFunctionMessage(name));
            }
        }

        private static bool IsDurableHttpTask(string functionName)
        {
            return string.Equals(functionName, HttpOptions.HttpTaskActivityReservedName);
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

        internal string GetInvalidEntityFunctionMessage(string name)
        {
            string message = $"The function '{name}' doesn't exist, is disabled, or is not an entity function. Additional info: ";
            if (this.knownOrchestrators.Keys.Count > 0)
            {
                message += $"The following are the known entity functions: '{string.Join("', '", this.knownEntities.Keys)}'.";
            }
            else
            {
                message += "No entity functions are currently registered!";
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

                        Stopwatch sw = Stopwatch.StartNew();
                        await this.defaultDurabilityProvider.CreateIfNotExistsAsync();
                        await this.taskHubWorker.StartAsync();

                        this.TraceHelper.ExtensionInformationalEvent(
                            this.Options.HubName,
                            instanceId: string.Empty,
                            functionName: string.Empty,
                            message: $"Task hub worker started. Latency: {sw.Elapsed}",
                            writeToUserLogs: true);

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
                // Wait to shut down the task hub worker until all function listeners have been shut down.
                if (this.isTaskHubWorkerStarted &&
                    !this.knownOrchestrators.Values.Any(info => info.HasActiveListener) &&
                    !this.knownActivities.Values.Any(info => info.HasActiveListener) &&
                    !this.knownEntities.Values.Any(info => info.HasActiveListener))
                {
                    bool isGracefulStop = this.Options.UseGracefulShutdown;

                    this.TraceHelper.ExtensionInformationalEvent(
                        this.Options.HubName,
                        instanceId: string.Empty,
                        functionName: string.Empty,
                        message: $"Stopping task hub worker. IsGracefulStop: {isGracefulStop}",
                        writeToUserLogs: true);

                    Stopwatch sw = Stopwatch.StartNew();
                    await this.taskHubWorker.StopAsync(isForced: !isGracefulStop);
                    this.isTaskHubWorkerStarted = false;

                    this.TraceHelper.ExtensionInformationalEvent(
                        this.Options.HubName,
                        instanceId: string.Empty,
                        functionName: string.Empty,
                        message: $"Task hub worker stopped. IsGracefulStop: {isGracefulStop}. Latency: {sw.Elapsed}",
                        writeToUserLogs: true);

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

        internal string GetExceptionTrace(string rawExceptionData)
        {
            if (rawExceptionData == null)
            {
                return "(null)";
            }
            else
            {
                return rawExceptionData;
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

#if !FUNCTIONS_V1
        /// <summary>
        /// Tags the current Activity with metadata: DurableFunctionsType, DurableFunctionsInstanceId, DurableFunctionsRuntimeStatus.
        /// This metadata will show up in Application Insights, if enabled.
        /// </summary>
        internal static void TagActivityWithOrchestrationStatus(OrchestrationRuntimeStatus status, string instanceId, bool isEntity = false)
        {
            // Adding "Tags" to activity allows using Application Insights to query current state of orchestrations
            Activity activity = Activity.Current;
            string functionsType = isEntity ? "Entity" : "Orchestrator";

            // The activity may be null when running unit tests, but should be non-null otherwise
            if (activity != null)
            {
                string statusStr = Enum.GetName(status.GetType(), status);
                activity.AddTag("DurableFunctionsType", functionsType);
                activity.AddTag("DurableFunctionsInstanceId", instanceId);
                activity.AddTag("DurableFunctionsRuntimeStatus", statusStr);
            }
        }
#endif
    }
}
