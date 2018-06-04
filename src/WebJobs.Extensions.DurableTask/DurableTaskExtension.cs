// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.AzureStorage;
using DurableTask.Core;
using DurableTask.Core.Middleware;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Configuration for the Durable Functions extension.
    /// </summary>
    public class DurableTaskExtension :
        IExtensionConfigProvider,
        IAsyncConverter<HttpRequestMessage, HttpResponseMessage>,
        INameVersionObjectManager<TaskOrchestration>,
        INameVersionObjectManager<TaskActivity>
    {
        /// <summary>
        /// The default task hub name to use when not explicitly configured.
        /// </summary>
        internal const string DefaultHubName = "DurableFunctionsHub";

        private static readonly string LoggerCategoryName = LogCategories.CreateTriggerCategory("DurableTask");

        // Creating client objects is expensive, so we cache them when the attributes match.
        // Note that OrchestrationClientAttribute defines a custom equality comparer.
        private readonly ConcurrentDictionary<OrchestrationClientAttribute, DurableOrchestrationClient> cachedClients =
            new ConcurrentDictionary<OrchestrationClientAttribute, DurableOrchestrationClient>();

        private readonly ConcurrentDictionary<FunctionName, OrchestratorInfo> registeredOrchestrators =
            new ConcurrentDictionary<FunctionName, OrchestratorInfo>();

        private readonly ConcurrentDictionary<FunctionName, ITriggeredFunctionExecutor> registeredActivities =
            new ConcurrentDictionary<FunctionName, ITriggeredFunctionExecutor>();

        private readonly AsyncLock taskHubLock = new AsyncLock();

        private AzureStorageOrchestrationService orchestrationService;
        private TaskHubWorker taskHubWorker;
        private bool isTaskHubWorkerStarted;

        private EndToEndTraceHelper traceHelper;
        private HttpApiHandler httpApiHandler;
        private LifeCycleNotificationHelper lifeCycleNotificationHelper;

        /// <summary>
        /// Gets or sets default task hub name to be used by all <see cref="DurableOrchestrationClient"/>,
        /// <see cref="DurableOrchestrationContext"/>, and <see cref="DurableActivityContext"/> instances.
        /// </summary>
        /// <remarks>
        /// A task hub is a logical grouping of storage resources. Alternate task hub names can be used to isolate
        /// multiple Durable Functions applications from each other, even if they are using the same storage backend.
        /// </remarks>
        /// <value>The name of the default task hub.</value>
        public string HubName { get; set; } = DefaultHubName;

        /// <summary>
        /// Gets or sets the number of messages to pull from the control queue at a time.
        /// </summary>
        /// <remarks>
        /// Messages pulled from the control queue are buffered in memory until the internal
        /// dispatcher is ready to process them.
        /// </remarks>
        /// <value>A positive integer configured by the host. The default value is <c>32</c>.</value>
        public int ControlQueueBatchSize { get; set; } = 32;

        /// <summary>
        /// Gets or sets the partition count for the control queue.
        /// </summary>
        /// <remarks>
        /// Increasing the number of partitions will increase the number of workers
        /// that can concurrently execute orchestrator functions. However, increasing
        /// the partition count can also increase the amount of load placed on the storage
        /// account and on the thread pool if the number of workers is smaller than the
        /// number of partitions.
        /// </remarks>
        /// <value>A positive integer between 1 and 16. The default value is <c>4</c>.</value>
        public int PartitionCount { get; set; } = 4;

        /// <summary>
        /// Gets or sets the visibility timeout of dequeued control queue messages.
        /// </summary>
        /// <value>
        /// A <c>TimeSpan</c> configured by the host. The default is 5 minutes.
        /// </value>
        public TimeSpan ControlQueueVisibilityTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the visibility timeout of dequeued work item queue messages.
        /// </summary>
        /// <value>
        /// A <c>TimeSpan</c> configured by the host. The default is 5 minutes.
        /// </value>
        public TimeSpan WorkItemQueueVisibilityTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the maximum number of activity functions that can be processed concurrently on a single host instance.
        /// </summary>
        /// <remarks>
        /// Increasing activity function concurrent can result in increased throughput but can
        /// also increase the total CPU and memory usage on a single worker instance.
        /// </remarks>
        /// <value>
        /// A positive integer configured by the host. The default value is 10X the number of processors on the current machine.
        /// </value>
        public int MaxConcurrentActivityFunctions { get; set; } = 10 * Environment.ProcessorCount;

        /// <summary>
        /// Gets or sets the maximum number of orchestrator functions that can be processed concurrently on a single host instance.
        /// </summary>
        /// <value>
        /// A positive integer configured by the host. The default value is 10X the number of processors on the current machine.
        /// </value>
        public int MaxConcurrentOrchestratorFunctions { get; set; } = 10 * Environment.ProcessorCount;

        /// <summary>
        /// Gets or sets the name of the Azure Storage connection string used to manage the underlying Azure Storage resources.
        /// </summary>
        /// <remarks>
        /// If not specified, the default behavior is to use the standard `AzureWebJobsStorage` connection string for all storage usage.
        /// </remarks>
        /// <value>The name of a connection string that exists in the app's application settings.</value>
        public string AzureStorageConnectionStringName { get; set; }

        /// <summary>
        /// Gets or sets the base URL for the HTTP APIs managed by this extension.
        /// </summary>
        /// <remarks>
        /// This property is intended for use only by runtime hosts.
        /// </remarks>
        /// <value>
        /// A URL pointing to the hosted function app that responds to status polling requests.
        /// </value>
        public Uri NotificationUrl { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to trace the inputs and outputs of function calls.
        /// </summary>
        /// <remarks>
        /// The default behavior when tracing function execution events is to include the number of bytes in the serialized
        /// inputs and outputs for function calls. This provides minimal information about what the inputs and outputs look
        /// like without bloating the logs or inadvertently exposing sensitive information to the logs. Setting
        /// <see cref="TraceInputsAndOutputs"/> to <c>true</c> will instead cause the default function logging to log
        /// the entire contents of function inputs and outputs.
        /// </remarks>
        /// <value>
        /// <c>true</c> to trace the raw values of inputs and outputs; otherwise <c>false</c>.
        /// </value>
        public bool TraceInputsAndOutputs { get; set; }

        /// <summary>
        /// Gets or sets the URL of an Azure Event Grid custom topic endpoint.
        /// When set, orchestration life cycle notification events will be automatically
        /// published to this endpoint.
        /// </summary>
        /// <remarks>
        /// Azure Event Grid topic URLs are generally expected to be in the form
        /// https://{topic_name}.{region}.eventgrid.azure.net/api/events.
        /// </remarks>
        /// <value>
        /// The Azure Event Grid custom topic URL.
        /// </value>
        public string EventGridTopicEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the name of the app setting containing the key used for authenticating with the Azure Event Grid custom topic at <see cref="EventGridTopicEndpoint"/>.
        /// </summary>
        /// <value>
        /// The name of the app setting that stores the Azure Event Grid key.
        /// </value>
        public string EventGridKeySettingName { get; set; }

        /// <summary>
        /// Gets or sets the Event Grid publish request retry count.
        /// </summary>
        public int EventGridPublishRetryCount { get; set; }

        /// <summary>
        /// Gets orsets the Event Grid publish request retry interval.
        /// </summary>
        public TimeSpan EventGridPublishRetryInterval { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the Event Grid publish request http status.
        /// </summary>
        /// <example>400,403</example>
        public int[] EventGridPublishRetryHttpStatus { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating whether to enable extended sessions.
        /// </summary>
        /// <remarks>
        /// <para>Extended sessions can improve the performance of orchestrator functions by allowing them to skip
        /// replays when new messages are received within short periods of time.</para>
        /// <para>Note that orchestrator functions which are extended this way will continue to count against the
        /// <see cref="MaxConcurrentOrchestratorFunctions"/> limit. To avoid starvation, only half of the maximum
        /// number of allowed concurrent orchestrator functions can be concurrently extended at any given time.
        /// The <see cref="ExtendedSessionIdleTimeoutInSeconds"/> property can also be used to control how long an idle
        /// orchestrator function is allowed to be extended.</para>
        /// <para>It is recommended that this property be set to <c>false</c> during development to help
        /// ensure that the orchestrator code correctly obeys the idempotency rules.</para>
        /// </remarks>
        /// <value>
        /// <c>true</c> to enable extended sessions; otherwise <c>false</c>.
        /// </value>
        public bool ExtendedSessionsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the amount of time in seconds before an idle session times out. The default value is 30 seconds.
        /// </summary>
        /// <remarks>
        /// This setting is applicable when <see cref="ExtendedSessionsEnabled"/> is set to <c>true</c>.
        /// </remarks>
        /// <value>
        /// The number of seconds before an idle session times out.
        /// </value>
        public int ExtendedSessionIdleTimeoutInSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets if logs for replay events need to be recorded.
        /// </summary>
        /// <remarks>
        /// The default value is false, which disables the logging of replay events.
        /// </remarks>
        /// <value>
        /// Boolean value specifying if the replay events should be logged.
        /// </value>
        public bool LogReplayEvents { get; set; }

        internal LifeCycleNotificationHelper LifeCycleNotificationHelper => this.lifeCycleNotificationHelper;

        internal EndToEndTraceHelper TraceHelper => this.traceHelper;

        /// <summary>
        /// Internal initialization call from the WebJobs host.
        /// </summary>
        /// <param name="context">Extension context provided by WebJobs.</param>
        void IExtensionConfigProvider.Initialize(ExtensionConfigContext context)
        {
            ConfigureLoaderHooks();

            context.ApplyConfig(this, "DurableTask");

            // Register the trigger bindings
            JobHostConfiguration hostConfig = context.Config;
            ILogger logger = context.Config.LoggerFactory.CreateLogger(LoggerCategoryName);

            this.traceHelper = new EndToEndTraceHelper(hostConfig, logger, this.LogReplayEvents);
            this.httpApiHandler = new HttpApiHandler(this, logger);

            this.lifeCycleNotificationHelper = new LifeCycleNotificationHelper(this, context);

            // Register the non-trigger bindings, which have a different model.
            var bindings = new BindingHelper(this, this.traceHelper);

            // For 202 support
            if (this.NotificationUrl == null)
            {
#pragma warning disable CS0618 // Type or member is obsolete
                this.NotificationUrl = context.GetWebhookHandler();
#pragma warning restore CS0618 // Type or member is obsolete
            }

            // Note that the order of the rules is important
            var rule = context.AddBindingRule<OrchestrationClientAttribute>()
                .AddConverter<string, StartOrchestrationArgs>(bindings.StringToStartOrchestrationArgs)
                .AddConverter<JObject, StartOrchestrationArgs>(bindings.JObjectToStartOrchestrationArgs);

            rule.BindToCollector<StartOrchestrationArgs>(bindings.CreateAsyncCollector);
            rule.BindToInput<DurableOrchestrationClient>(this.GetClient);

            context.AddBindingRule<OrchestrationTriggerAttribute>()
                .BindToTrigger(new OrchestrationTriggerAttributeBindingProvider(this, context, this.traceHelper));

            context.AddBindingRule<ActivityTriggerAttribute>()
                .BindToTrigger(new ActivityTriggerAttributeBindingProvider(this, context, this.traceHelper));

            AzureStorageOrchestrationServiceSettings settings = this.GetOrchestrationServiceSettings();
            this.orchestrationService = new AzureStorageOrchestrationService(settings);
            this.taskHubWorker = new TaskHubWorker(this.orchestrationService, this, this);
            this.taskHubWorker.AddOrchestrationDispatcherMiddleware(this.OrchestrationMiddleware);

            context.Config.AddService<IOrchestrationService>(this.orchestrationService);
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

            ITriggeredFunctionExecutor executor;
            if (!this.registeredActivities.TryGetValue(activityFunction, out executor))
            {
                throw new InvalidOperationException($"Activity function '{activityFunction}' does not exist.");
            }

            return new TaskActivityShim(this, executor, name);
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

            context.History = orchestrationRuntimeState.Events;
            context.SetInput(orchestrationRuntimeState.Input);

            FunctionName orchestratorFunction = new FunctionName(context.Name);

            OrchestratorInfo info;
            if (!this.registeredOrchestrators.TryGetValue(orchestratorFunction, out info))
            {
                throw new InvalidOperationException($"Orchestrator function '{orchestratorFunction}' does not exist.");
            }

            // 1. Start the functions invocation pipeline (billing, logging, bindings, and timeout tracking).
            FunctionResult result = await info.Executor.TryExecuteAsync(
                new TriggeredFunctionData
                {
                    TriggerValue = context,

#pragma warning disable CS0618 // Approved for use by this extension
                    InvokeHandler = userCodeInvoker =>
                    {
                        // 2. Configure the shim with the inner invoker to execute the user code.
                        shim.SetFunctionInvocationCallback(userCodeInvoker);

                        // 3. Move to the next stage of the DTFx pipeline to trigger the orchestrator shim.
                        return next();
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
                    var innerClient = new AzureStorageOrchestrationService(settings);
                    return new DurableOrchestrationClient(innerClient, this, attr, this.traceHelper);
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
            string connectionName = connectionNameOverride ?? this.AzureStorageConnectionStringName ?? ConnectionStringNames.Storage;
            string resolvedStorageConnectionString = AmbientConnectionStringProvider.Instance.GetConnectionString(connectionName);

            if (string.IsNullOrEmpty(resolvedStorageConnectionString))
            {
                throw new InvalidOperationException("Unable to find an Azure Storage connection string to use for this binding.");
            }

            TimeSpan extendedSessionTimeout = TimeSpan.FromSeconds(
                Math.Max(this.ExtendedSessionIdleTimeoutInSeconds, 0));

            return new AzureStorageOrchestrationServiceSettings
            {
                StorageConnectionString = resolvedStorageConnectionString,
                TaskHubName = taskHubNameOverride ?? this.HubName,
                PartitionCount = this.PartitionCount,
                ControlQueueBatchSize = this.ControlQueueBatchSize,
                ControlQueueVisibilityTimeout = this.ControlQueueVisibilityTimeout,
                WorkItemQueueVisibilityTimeout = this.WorkItemQueueVisibilityTimeout,
                MaxConcurrentTaskOrchestrationWorkItems = this.MaxConcurrentOrchestratorFunctions,
                MaxConcurrentTaskActivityWorkItems = this.MaxConcurrentActivityFunctions,
                ExtendedSessionsEnabled = this.ExtendedSessionsEnabled,
                ExtendedSessionIdleTimeout = extendedSessionTimeout,
            };
        }

        internal void RegisterOrchestrator(FunctionName orchestratorFunction, OrchestratorInfo orchestratorInfo)
        {
            if (!this.registeredOrchestrators.TryUpdate(orchestratorFunction, orchestratorInfo, null))
            {
                if (!this.registeredOrchestrators.TryAdd(orchestratorFunction, orchestratorInfo))
                {
                    throw new ArgumentException(
                        $"The orchestrator function named '{orchestratorFunction}' is already registered.");
                }
            }
        }

        internal void DeregisterOrchestrator(FunctionName orchestratorFunction)
        {
            this.registeredOrchestrators.TryRemove(orchestratorFunction, out _);
        }

        internal OrchestratorInfo GetOrchestratorInfo(FunctionName orchestratorFunction)
        {
            OrchestratorInfo info;
            this.registeredOrchestrators.TryGetValue(orchestratorFunction, out info);

            return info;
        }

        internal void RegisterActivity(FunctionName activityFunction, ITriggeredFunctionExecutor executor)
        {
            // Allow adding with a null key and subsequently updating with a non-null key.
            if (!this.registeredActivities.TryUpdate(activityFunction, executor, null))
            {
                if (!this.registeredActivities.TryAdd(activityFunction, executor))
                {
                    throw new ArgumentException($"The activity function named '{activityFunction}' is already registered.");
                }
            }
        }

        internal void DeregisterActivity(FunctionName activityFunction)
        {
            this.registeredActivities.TryRemove(activityFunction, out _);
        }

        internal void AssertOrchestratorExists(string name, string version)
        {
            var functionName = new FunctionName(name);
            if (!this.registeredOrchestrators.ContainsKey(functionName))
            {
                throw new ArgumentException(
                    string.Format(
                        "The function '{0}' doesn't exist, is disabled, or is not an orchestrator function. The following are the active orchestrator functions: {1}.",
                        functionName,
                        string.Join(", ", this.registeredOrchestrators.Keys)));
            }
        }

        internal FunctionType ThrowIfInvalidFunctionType(string name, FunctionType functionType, string version)
        {
            var functionName = new FunctionName(name);

            if (functionType == FunctionType.Activity)
            {
                if (this.registeredActivities.ContainsKey(functionName))
                {
                    return FunctionType.Activity;
                }

                throw new ArgumentException(
                        string.Format(
                            "The function '{0}' doesn't exist, is disabled, or is not an activity function. The following are the active activity functions: '{1}'",
                            functionName,
                            string.Join(", ", this.registeredActivities.Keys)));
            }

            if (functionType == FunctionType.Orchestrator)
            {
                if (this.registeredOrchestrators.ContainsKey(functionName))
                {
                    return FunctionType.Orchestrator;
                }

                throw new ArgumentException(
                    string.Format(
                        "The function '{0}' doesn't exist, is disabled, or is not an orchestrator function. The following are the active orchestrator functions: '{1}'",
                        functionName,
                        string.Join(", ", this.registeredOrchestrators.Keys)));
            }

            throw new ArgumentException(
                string.Format(
                    "The function '{0}' doesn't exist, is disabled, or is not an activity or orchestrator function. The following are the active activity functions: '{1}', orchestrator functions: '{2}'",
                    functionName,
                    string.Join(", ", this.registeredActivities.Keys),
                    string.Join(", ", this.registeredOrchestrators.Keys)));
        }

        internal async Task<bool> StartTaskHubWorkerIfNotStartedAsync()
        {
            if (!this.isTaskHubWorkerStarted)
            {
                using (await this.taskHubLock.AcquireAsync())
                {
                    if (!this.isTaskHubWorkerStarted)
                    {
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
                if (!this.isTaskHubWorkerStarted &&
                    this.registeredOrchestrators.Count == 0 &&
                    this.registeredActivities.Count == 0)
                {
                    await this.taskHubWorker.StopAsync(isForced: true);
                    this.isTaskHubWorkerStarted = false;
                    return true;
                }
            }

            return false;
        }

        internal string GetIntputOutputTrace(string rawInputOutputData)
        {
            if (this.TraceInputsAndOutputs)
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
            return this.httpApiHandler.CreateCheckStatusResponse(request, instanceId, attribute);
        }

        // Get a data structure containing status, terminate and send external event HTTP.
        internal HttpManagementPayload CreateHttpManagementPayload(
            string instanceId,
            string taskHubName,
            string connectionName)
        {
            return this.httpApiHandler.CreateHttpManagementPayload(instanceId, taskHubName, connectionName);
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
            return await this.httpApiHandler.WaitForCompletionOrCreateCheckStatusResponseAsync(
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
            return this.httpApiHandler.HandleRequestAsync(request);
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
