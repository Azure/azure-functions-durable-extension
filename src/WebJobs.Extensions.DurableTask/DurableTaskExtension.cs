// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.AzureStorage;
using DurableTask.Core;
using DurableTask.Core.Middleware;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Configuration for the Durable Functions extension.
    /// </summary>
#if NETSTANDARD2_0
    [Extension("DurableTask", "DurableTask")]
#endif
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

        private readonly ConcurrentDictionary<FunctionName, RegisteredFunctionInfo> knownActivities =
            new ConcurrentDictionary<FunctionName, RegisteredFunctionInfo>();

        private readonly AsyncLock taskHubLock = new AsyncLock();

        private readonly bool isOptionsConfigured;

        private INameResolver nameResolver;
        private AzureStorageOrchestrationService orchestrationService;
        private AzureStorageOrchestrationServiceSettings orchestrationServiceSettings;
        private TaskHubWorker taskHubWorker;
        private IConnectionStringResolver connectionStringResolver;
        private bool isTaskHubWorkerStarted;

#if !NETSTANDARD2_0
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
        /// <param name="connectionStringResolver">The resolver to use for looking up connection strings.</param>
        public DurableTaskExtension(
            IOptions<DurableTaskOptions> options,
            ILoggerFactory loggerFactory,
            INameResolver nameResolver,
            IConnectionStringResolver connectionStringResolver)
        {
            // Options will be null in Functions v1 runtime - populated later.
            this.Options = options?.Value ?? new DurableTaskOptions();
            this.nameResolver = nameResolver ?? throw new ArgumentNullException(nameof(nameResolver));
            this.connectionStringResolver = connectionStringResolver ?? throw new ArgumentNullException(nameof(connectionStringResolver));

            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            ILogger logger = loggerFactory.CreateLogger(LoggerCategoryName);

            this.TraceHelper = new EndToEndTraceHelper(logger, this.Options.LogReplayEvents);
            this.HttpApiHandler = new HttpApiHandler(this, logger);
            this.LifeCycleNotificationHelper = this.CreateLifeCycleNotificationHelper();
            this.isOptionsConfigured = true;
        }

#if !NETSTANDARD2_0
        /// <summary>
        /// Gets or sets default task hub name to be used by all <see cref="DurableOrchestrationClient"/>,
        /// <see cref="DurableOrchestrationContext"/>, and <see cref="DurableActivityContext"/> instances.
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
                .AddConverter<DurableOrchestrationClient, string>(bindings.DurableOrchestrationClientToString);

            rule.BindToCollector<StartOrchestrationArgs>(bindings.CreateAsyncCollector);
            rule.BindToInput<DurableOrchestrationClient>(this.GetClient);

            context.AddBindingRule<OrchestrationTriggerAttribute>()
                .BindToTrigger(new OrchestrationTriggerAttributeBindingProvider(this, context, this.TraceHelper));

            context.AddBindingRule<ActivityTriggerAttribute>()
                .BindToTrigger(new ActivityTriggerAttributeBindingProvider(this, context, this.TraceHelper));

            this.orchestrationServiceSettings = this.GetOrchestrationServiceSettings();
            this.orchestrationService = new AzureStorageOrchestrationService(this.orchestrationServiceSettings);
            this.taskHubWorker = new TaskHubWorker(this.orchestrationService, this, this);
            this.taskHubWorker.AddOrchestrationDispatcherMiddleware(this.OrchestrationMiddleware);
        }

        private void InitializeForFunctionsV1(ExtensionConfigContext context)
        {
#if !NETSTANDARD2_0
            context.ApplyConfig(this.Options, "DurableTask");

            ILogger logger = context.Config.LoggerFactory.CreateLogger(LoggerCategoryName);

            this.TraceHelper = new EndToEndTraceHelper(logger, this.Options.LogReplayEvents);
            this.HttpApiHandler = new HttpApiHandler(this, logger);
            this.connectionStringResolver = new WebJobsConnectionStringProvider();
            this.nameResolver = context.Config.NameResolver;
            this.LifeCycleNotificationHelper = this.CreateLifeCycleNotificationHelper();            
#endif
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
            if (!string.IsNullOrEmpty(this.Options.EventGridTopicEndpoint) || !string.IsNullOrEmpty(this.Options.EventGridKeySettingName))
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
            var context = new DurableOrchestrationContext(this, name);
            return new TaskOrchestrationShim(this, context);
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
            TaskOrchestrationShim shim = (TaskOrchestrationShim)dispatchContext.GetProperty<TaskOrchestration>();
            DurableOrchestrationContext context = shim.Context;

            OrchestrationRuntimeState orchestrationRuntimeState = dispatchContext.GetProperty<OrchestrationRuntimeState>();

            if (orchestrationRuntimeState.ParentInstance != null)
            {
                context.ParentInstanceId = orchestrationRuntimeState.ParentInstance.OrchestrationInstance.InstanceId;
            }

            context.InstanceId = orchestrationRuntimeState.OrchestrationInstance.InstanceId;
            context.IsReplaying = orchestrationRuntimeState.ExecutionStartedEvent.IsPlayed;
            context.History = orchestrationRuntimeState.Events;
            context.SetInput(orchestrationRuntimeState.Input);

            FunctionName orchestratorFunction = new FunctionName(context.Name);

            RegisteredFunctionInfo info;
            if (!this.knownOrchestrators.TryGetValue(orchestratorFunction, out info))
            {
                string message = this.GetInvalidOrchestratorFunctionMessage(orchestratorFunction.Name);
                this.TraceHelper.ExtensionWarningEvent(
                    this.Options.HubName,
                    orchestratorFunction.Name,
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
                this.TraceHelper.FunctionAwaited(
                    context.HubName,
                    context.Name,
                    context.InstanceId,
                    context.IsReplaying);
            }

            if (context.IsCompleted &&
                context.ContinuedAsNew &&
                context.PreserveUnprocessedEvents)
            {
                // Reschedule any unprocessed external events so that they can be picked up
                // in the next iteration.
                context.RescheduleBufferedExternalEvents();
            }

            await context.RunDeferredTasks();
        }

        // This is temporary until script loading
        private static void ConfigureLoaderHooks()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
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
        /// Gets a <see cref="DurableOrchestrationClient"/> using configuration from a <see cref="OrchestrationClientAttribute"/> instance.
        /// </summary>
        /// <param name="attribute">The attribute containing the client configuration parameters.</param>
        /// <returns>Returns a <see cref="DurableOrchestrationClient"/> instance. The returned instance may be a cached instance.</returns>
        protected internal virtual DurableOrchestrationClient GetClient(OrchestrationClientAttribute attribute)
        {
            DurableOrchestrationClient client = this.cachedClients.GetOrAdd(
                attribute,
                attr =>
                {
                    AzureStorageOrchestrationServiceSettings settings = this.GetOrchestrationServiceSettings(attr);

                    AzureStorageOrchestrationService innerClient;
                    if (this.orchestrationServiceSettings != null &&
                        this.orchestrationService != null &&
                        string.Equals(this.orchestrationServiceSettings.TaskHubName, settings.TaskHubName, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(this.orchestrationServiceSettings.StorageConnectionString, settings.StorageConnectionString, StringComparison.OrdinalIgnoreCase))
                    {
                        // It's important that clients use the same AzureStorageOrchestrationService instance
                        // as the host when possible to ensure we any send operations can be picked up
                        // immediately instead of waiting for the next queue polling interval.
                        innerClient = this.orchestrationService;
                    }
                    else
                    {
                        innerClient = new AzureStorageOrchestrationService(settings);
                    }

                    return new DurableOrchestrationClient(innerClient, this, attr);
                });

            return client;
        }

        internal AzureStorageOrchestrationServiceSettings GetOrchestrationServiceSettings(
                OrchestrationClientAttribute attribute)
        {
            return this.GetOrchestrationServiceSettings(
                connectionNameOverride: attribute.ConnectionName,
                taskHubNameOverride: attribute.TaskHub);
        }

        internal AzureStorageOrchestrationServiceSettings GetOrchestrationServiceSettings(
            string connectionNameOverride = null,
            string taskHubNameOverride = null)
        {
            string connectionName = connectionNameOverride ?? this.Options.AzureStorageConnectionStringName ?? ConnectionStringNames.Storage;
            string resolvedStorageConnectionString = this.connectionStringResolver.Resolve(connectionName);

            if (string.IsNullOrEmpty(resolvedStorageConnectionString))
            {
                throw new InvalidOperationException("Unable to find an Azure Storage connection string to use for this binding.");
            }

            TimeSpan extendedSessionTimeout = TimeSpan.FromSeconds(
                Math.Max(this.Options.ExtendedSessionIdleTimeoutInSeconds, 0));

            var settings = new AzureStorageOrchestrationServiceSettings
            {
                StorageConnectionString = resolvedStorageConnectionString,
                TaskHubName = taskHubNameOverride ?? this.Options.HubName,
                PartitionCount = this.Options.PartitionCount,
                ControlQueueBatchSize = this.Options.ControlQueueBatchSize,
                ControlQueueVisibilityTimeout = this.Options.ControlQueueVisibilityTimeout,
                WorkItemQueueVisibilityTimeout = this.Options.WorkItemQueueVisibilityTimeout,
                MaxConcurrentTaskOrchestrationWorkItems = this.Options.MaxConcurrentOrchestratorFunctions,
                MaxConcurrentTaskActivityWorkItems = this.Options.MaxConcurrentActivityFunctions,
                ExtendedSessionsEnabled = this.Options.ExtendedSessionsEnabled,
                ExtendedSessionIdleTimeout = extendedSessionTimeout,
                MaxQueuePollingInterval = this.Options.MaxQueuePollingInterval,
                TrackingStoreStorageAccountDetails = this.GetStorageAccountDetailsOrNull(
                    this.Options.TrackingStoreConnectionStringName),
            };

            if (!string.IsNullOrEmpty(this.Options.TrackingStoreNamePrefix))
            {
                settings.TrackingStoreNamePrefix = this.Options.TrackingStoreNamePrefix;
            }

            return settings;
        }

        private StorageAccountDetails GetStorageAccountDetailsOrNull(string connectionName)
        {
            if (string.IsNullOrEmpty(connectionName))
            {
                return null;
            }

            string resolvedStorageConnectionString = this.connectionStringResolver.Resolve(connectionName);
            if (string.IsNullOrEmpty(resolvedStorageConnectionString))
            {
                throw new InvalidOperationException($"Unable to resolve the Azure Storage connection named '{connectionName}'.");
            }

            return new StorageAccountDetails
            {
                ConnectionString = resolvedStorageConnectionString,
            };
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

        internal RegisteredFunctionInfo GetOrchestratorInfo(FunctionName orchestratorFunction)
        {
            RegisteredFunctionInfo info;
            this.knownOrchestrators.TryGetValue(orchestratorFunction, out info);

            return info;
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
            if (this.Options.TraceInputsAndOutputs)
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

        // Get a response that will point to our webhook handler.
        internal HttpResponseMessage CreateCheckStatusResponse(
            HttpRequestMessage request,
            string instanceId,
            OrchestrationClientAttribute attribute)
        {
            return this.HttpApiHandler.CreateCheckStatusResponse(request, instanceId, attribute);
        }

        // Get a data structure containing status, terminate and send external event HTTP.
        internal HttpManagementPayload CreateHttpManagementPayload(
            string instanceId,
            string taskHubName,
            string connectionName)
        {
            return this.HttpApiHandler.CreateHttpManagementPayload(instanceId, taskHubName, connectionName);
        }

        // Get a response that will wait for response from the durable function for predefined period of time before
        // pointing to our webhook handler.
        internal async Task<HttpResponseMessage> WaitForCompletionOrCreateCheckStatusResponseAsync(
            HttpRequestMessage request,
            string instanceId,
            OrchestrationClientAttribute attribute,
            TimeSpan timeout,
            TimeSpan retryInterval)
        {
            return await this.HttpApiHandler.WaitForCompletionOrCreateCheckStatusResponseAsync(
                request,
                instanceId,
                attribute,
                timeout,
                retryInterval);
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
