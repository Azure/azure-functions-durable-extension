﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Net;
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
        // Creating client objects is expensive, so we cache them when the attributes match.
        // Note that OrchestrationClientAttribute defines a custom equality comparer.
        private readonly ConcurrentDictionary<OrchestrationClientAttribute, DurableOrchestrationClient> cachedClients =
            new ConcurrentDictionary<OrchestrationClientAttribute, DurableOrchestrationClient>();

        private readonly ConcurrentDictionary<FunctionName, ITriggeredFunctionExecutor> registeredOrchestrators = 
            new ConcurrentDictionary<FunctionName, ITriggeredFunctionExecutor>();
        private readonly ConcurrentDictionary<FunctionName, ITriggeredFunctionExecutor> registeredActivities = 
            new ConcurrentDictionary<FunctionName, ITriggeredFunctionExecutor>();

        private readonly AsyncLock taskHubLock = new AsyncLock();

        private AzureStorageOrchestrationService orchestrationService;
        private TaskHubWorker taskHubWorker;
        private bool isTaskHubWorkerStarted;

        private EndToEndTraceHelper traceHelper;
        private HttpApiHandler httpApiHandler;

        /// <summary>
        /// The default task hub name to use when not explicitly configured.
        /// </summary>
        internal const string DefaultHubName = "DurableFunctionsHub";

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
        /// <value>A positive integer configured by the host. The default value is <c>20</c>.</value>
        public int ControlQueueBatchSize { get; set; } = 20;

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
        /// Gets or sets the maximum number of work items that can be processed concurrently on a single node.
        /// </summary>
        /// <value>
        /// A positive integer configured by the host. The default value is 10.
        /// </value>
        public int MaxConcurrentTaskActivityWorkItems { get; set; } = 10;

        /// <summary>
        /// Gets or sets the maximum number of orchestrations that can be processed concurrently on a single node.
        /// </summary>
        /// <value>
        /// A positive integer configured by the host. The default value is 100.
        /// </value>
        public int MaxConcurrentTaskOrchestrationWorkItems { get; set; } = 100;

        /// <summary>
        /// Gets or sets the name of the Azure Storage connection string used to manage the underlying Azure Storage resources.
        /// </summary>
        /// <remarks>
        /// If not specified, the default behavior is to use the standard `AzureWebJobsStorage` connection string for all storage usage.
        /// </remarks>
        /// <value>The name of a connection string that exists in the app's application settings.</value>
        public string AzureStorageConnectionStringName { get; set; }

        /// <summary>
        /// The notification URL for polling status of instances. 
        /// </summary>
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
        /// Gets or sets a value indicating whether to expose HTTP APIs for managing orchestration instances.
        /// </summary>
        /// <remarks>
        /// Orchestration instances can be managed using HTTP APIs implemented by the Durable Functions extension.
        /// This includes checking status, raising events, and terminating instances. These APIs do not require
        /// any authentication and therefore the instance IDs for these URLs should not be shared externally.
        /// These APIs are enabled by default but can be disabled by setting this property to <c>true</c>.
        /// </remarks>
        /// <value>
        /// <c>true</c> to disable the instance management HTTP APIs; otherwise <c>false</c>.
        /// </value>
        public bool DisableHttpManagementApis { get; set; }

        internal EndToEndTraceHelper TraceHelper => this.traceHelper;

        void IExtensionConfigProvider.Initialize(ExtensionConfigContext context)
        {
            ConfigureLoaderHooks();

            context.ApplyConfig(this, "DurableTask");

            // Register the trigger bindings
            JobHostConfiguration hostConfig = context.Config;

            this.traceHelper = new EndToEndTraceHelper(hostConfig, context.Trace);
            this.httpApiHandler = new HttpApiHandler(this, context.Trace);

            // Register the non-trigger bindings, which have a different model.
            var bindings = new BindingHelper(this, this.traceHelper);

            // For 202 support
            if (this.NotificationUrl == null)
            {
                this.NotificationUrl = context.GetWebhookHandler();
            }

            // Note that the order of the rules is important
            var rule = context.AddBindingRule<OrchestrationClientAttribute>()
                .AddConverter<JObject, StartOrchestrationArgs>(bindings.JObjectToStartOrchestrationArgs);

            rule.BindToCollector<StartOrchestrationArgs>(bindings.CreateAsyncCollector);
            rule.BindToInput<DurableOrchestrationClient>(GetClient);
                
            context.AddBindingRule<OrchestrationTriggerAttribute>()
                .BindToTrigger(new OrchestrationTriggerAttributeBindingProvider(this, context, this.traceHelper));

            context.AddBindingRule<ActivityTriggerAttribute>()
                .BindToTrigger(new ActivityTriggerAttributeBindingProvider(this, context, this.traceHelper));

            AzureStorageOrchestrationServiceSettings settings = this.GetOrchestrationServiceSettings();
            this.orchestrationService = new AzureStorageOrchestrationService(settings);
            this.taskHubWorker = new TaskHubWorker(this.orchestrationService, this, this);
            this.taskHubWorker.AddOrchestrationDispatcherMiddleware(this.OrchestrationMiddleware);
        }

        void INameVersionObjectManager<TaskOrchestration>.Add(ObjectCreator<TaskOrchestration> creator)
        {
            throw new InvalidOperationException("Orchestrations should never be added explicitly.");
        }

        TaskOrchestration INameVersionObjectManager<TaskOrchestration>.GetObject(string name, string version)
        {
            var context = new DurableOrchestrationContext(this, name, version);
            return new TaskOrchestrationShim(this, context);
        }

        void INameVersionObjectManager<TaskActivity>.Add(ObjectCreator<TaskActivity> creator)
        {
            throw new InvalidOperationException("Activities should never be added explicitly.");
        }

        TaskActivity INameVersionObjectManager<TaskActivity>.GetObject(string name, string version)
        {
            FunctionName activityFunction = new FunctionName(name, version);

            ITriggeredFunctionExecutor executor;
            if (!this.registeredActivities.TryGetValue(activityFunction, out executor))
            {
                throw new InvalidOperationException($"Activity function '{activityFunction}' does not exist.");
            }

            return new TaskActivityShim(this, executor, name, version);
        }

        private async Task OrchestrationMiddleware(DispatchMiddlewareContext dispatchContext, Func<Task> next)
        {
            TaskOrchestrationShim shim = (TaskOrchestrationShim)dispatchContext.GetProperty<TaskOrchestration>();
            DurableOrchestrationContext context = shim.Context;

            FunctionName orchestratorFunction = new FunctionName(context.Name, context.Version);

            ITriggeredFunctionExecutor executor;
            if (!this.registeredOrchestrators.TryGetValue(orchestratorFunction, out executor))
            {
                throw new InvalidOperationException($"Orchestrator function '{orchestratorFunction}' does not exist.");
            }

            // 1. Start the functions invocation pipeline (billing, logging, bindings, and timeout tracking).
            FunctionResult result = await executor.TryExecuteAsync(
                new TriggeredFunctionData
                {
                    TriggerValue = context,
                    InvokeHandler = userCodeInvoker =>
                    {
                        // 2. Configure the shim with the inner invoker to execute the user code.
                        shim.SetFunctionInvocationCallback(userCodeInvoker);

                        // 3. Move to the next stage of the DTFx pipeline to trigger the orchestrator shim.
                        return next();
                    }
                },
                CancellationToken.None);

            if (!context.IsCompleted)
            {
                this.TraceHelper.FunctionAwaited(
                    context.HubName,
                    context.Name,
                    context.Version,
                    context.InstanceId,
                    context.IsReplaying);
            }
        }

        // This is temporary until script loading 
        private static void ConfigureLoaderHooks()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
        }

        private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            var appDomain = (AppDomain)sender;
            string assemblyName = appDomain.ApplyPolicy(args.Name);

            // Fallback.
            var shortName = new AssemblyName(assemblyName).Name;
            foreach (var assembly in appDomain.GetAssemblies())
            {
                if (string.Equals(assembly.GetName().Name, shortName, StringComparison.OrdinalIgnoreCase))
                {
                    return assembly;
                }
            }
            return null;
        }

        internal DurableOrchestrationClient GetClient(OrchestrationClientAttribute attribute)
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
            return GetOrchestrationServiceSettings(
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

            return new AzureStorageOrchestrationServiceSettings
            {
                StorageConnectionString = resolvedStorageConnectionString,
                TaskHubName = taskHubNameOverride ?? this.HubName,
                ControlQueueVisibilityTimeout = this.ControlQueueVisibilityTimeout,
                WorkItemQueueVisibilityTimeout = this.WorkItemQueueVisibilityTimeout,
                MaxConcurrentTaskOrchestrationWorkItems = this.MaxConcurrentTaskOrchestrationWorkItems,
                MaxConcurrentTaskActivityWorkItems = this.MaxConcurrentTaskActivityWorkItems,
            };
        }

        internal void RegisterOrchestrator(FunctionName orchestratorFunction, ITriggeredFunctionExecutor executor)
        {
            if (!this.registeredOrchestrators.TryAdd(orchestratorFunction, executor))
            {
                throw new ArgumentException($"The orchestrator function named '{orchestratorFunction}' is already registered.");
            }
        }

        internal void DeregisterOrchestrator(FunctionName orchestratorFunction)
        {
            this.registeredOrchestrators.TryRemove(orchestratorFunction, out _);
        }

        internal void RegisterActivity(FunctionName activityFunction, ITriggeredFunctionExecutor executor)
        {
            if (!this.registeredActivities.TryAdd(activityFunction, executor))
            {
                throw new ArgumentException($"The activity function named '{activityFunction}' is already registered.");
            }
        }

        internal void DeregisterActivity(FunctionName activityFunction)
        {
            this.registeredActivities.TryRemove(activityFunction, out _);
        }

        internal void AssertOrchestratorExists(string name, string version)
        {
            var functionName = new FunctionName(name, version);
            if (!this.registeredOrchestrators.ContainsKey(functionName))
            {
                throw new ArgumentException(
                    string.Format("The function '{0}' doesn't exist, is disabled, or is not an orchestrator function. The following are the active orchestrator functions: {1}.",
                        functionName,
                        string.Join(", ", this.registeredOrchestrators.Keys)));
            }
        }

        internal FunctionType ThrowIfInvalidFunctionType(string name, FunctionType functionType, string version)
        {
            var functionName = new FunctionName(name, version);

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
                return "(" + Encoding.UTF8.GetByteCount(rawInputOutputData) + " bytes)";
            }
        }

        // Get a response that will point to our webhook handler. 
        internal HttpResponseMessage CreateCheckStatusResponse(
            HttpRequestMessage request,
            string instanceId,
            OrchestrationClientAttribute attribute)
        {
            if (this.DisableHttpManagementApis)
            {
                throw new InvalidOperationException("HTTP instance management APIs are disabled.");
            }

            return this.httpApiHandler.CreateCheckStatusResponse(request, instanceId, attribute);
        }

        Task<HttpResponseMessage> IAsyncConverter<HttpRequestMessage, HttpResponseMessage>.ConvertAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (this.DisableHttpManagementApis)
            {
                return Task.FromResult(request.CreateResponse(HttpStatusCode.NotFound));
            }

            return this.httpApiHandler.HandleRequestAsync(request);
        }
    }
}
