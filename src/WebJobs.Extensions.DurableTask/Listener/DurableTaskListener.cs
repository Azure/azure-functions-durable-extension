// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.AzureStorage;
using DurableTask.Core;
using DurableTask.Core.Middleware;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal sealed class DurableTaskListener : 
        IListener,
        INameVersionObjectManager<TaskOrchestration>,
        INameVersionObjectManager<TaskActivity>
    {
        private static readonly object singletonInitLock = new object();
        private static readonly AsyncLock singletonLifecycleLock = new AsyncLock();
        private static ConcurrentDictionary<string, ITriggeredFunctionExecutor> orchestratorFunctionExecutors =
            new ConcurrentDictionary<string, ITriggeredFunctionExecutor>(StringComparer.OrdinalIgnoreCase);
        private static ConcurrentDictionary<string, ITriggeredFunctionExecutor> activityFunctionExecutors =
            new ConcurrentDictionary<string, ITriggeredFunctionExecutor>(StringComparer.OrdinalIgnoreCase);

        private static AzureStorageOrchestrationService sharedOrchestrationService;
        private static TaskHubWorker sharedWorker;
        private static string sharedWorkerHubName;
        private static bool isSharedWorkerStarted;

        private readonly DurableTaskConfiguration config;

        private DurableTaskListener(DurableTaskConfiguration config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));

            lock (singletonInitLock)
            {
                string taskHubName = config.HubName;
                if (sharedWorker == null)
                {
                    sharedOrchestrationService = new AzureStorageOrchestrationService(config.GetOrchestrationServiceSettings());
                    sharedWorker = new TaskHubWorker(sharedOrchestrationService, this, this);
                    sharedWorker.AddOrchestrationDispatcherMiddleware(this.OrchestrationMiddleware);
                    sharedWorkerHubName = taskHubName;
                }
                else if (!string.Equals(sharedWorkerHubName, taskHubName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Only one task hub is supported in a single application.");
                }
            }
        }

        public static DurableTaskListener CreateForOrchestration(
            DurableTaskConfiguration config,
            string orchestratorName,
            string orchestratorVersion,
            ITriggeredFunctionExecutor orchestratorFunctionExecutor)
        {
            if (orchestratorFunctionExecutor == null)
            {
                throw new ArgumentNullException(nameof(orchestratorFunctionExecutor));
            }

            string key = GetFunctionKey(orchestratorName, orchestratorVersion);
            if (!orchestratorFunctionExecutors.TryAdd(key, orchestratorFunctionExecutor))
            {
                // Best effort for now until we can figure out how to properly remove registrations when the listener is recycled.
            }

            return new DurableTaskListener(config);
        }

        public static DurableTaskListener CreateForActivity(
            DurableTaskConfiguration config,
            string activityName,
            string activityVersion,
            ITriggeredFunctionExecutor activityFunctionExecutor)
        {
            if (activityFunctionExecutor == null)
            {
                throw new ArgumentNullException(nameof(activityFunctionExecutor));
            }

            string key = GetFunctionKey(activityName, activityVersion);
            if (!activityFunctionExecutors.TryAdd(key, activityFunctionExecutor))
            {
                // Best effort for now until we can figure out how to properly remove registrations when the listener is recycled.
            }

            return new DurableTaskListener(config);
        }

        #region Listener Lifecycle Operations
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using (await singletonLifecycleLock.AcquireAsync())
            {
                if (!isSharedWorkerStarted)
                {
                    await sharedOrchestrationService.CreateIfNotExistsAsync();
                    await sharedWorker.StartAsync();
                    isSharedWorkerStarted = true;
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            using (await singletonLifecycleLock.AcquireAsync())
            {
                if (isSharedWorkerStarted)
                {
                    // TODO: Shut down the shared worker. This is currently broken.
                }
            }
        }

        public void Cancel()
        {
            // TODO: Shut down the shared worker. This is currently broken.
        }

        public void Dispose()
        {
        }
        #endregion

        void INameVersionObjectManager<TaskOrchestration>.Add(ObjectCreator<TaskOrchestration> creator)
        {
            throw new InvalidOperationException("Orchestrations should never be added explicitly.");
        }

        TaskOrchestration INameVersionObjectManager<TaskOrchestration>.GetObject(string name, string version)
        {
            var context = new DurableOrchestrationContext(this.config.HubName, name, version, this.config.TraceHelper);
            return new TaskOrchestrationShim(this.config, context);
        }

        void INameVersionObjectManager<TaskActivity>.Add(ObjectCreator<TaskActivity> creator)
        {
            throw new InvalidOperationException("Activities should never be added explicitly.");
        }

        TaskActivity INameVersionObjectManager<TaskActivity>.GetObject(string name, string version)
        {
            string key = GetFunctionKey(name, version);

            ITriggeredFunctionExecutor executor;
            if (!activityFunctionExecutors.TryGetValue(key, out executor))
            {
                throw new ArgumentException($"No activity function named '{name}' with version '{version}' is registered.");
            }

            return new TaskActivityShim(this.config, executor, name, version);
        }

        private static string GetFunctionKey(object orchestratorName, object orchestratorVersion)
        {
            return orchestratorName + "/" + orchestratorVersion;
        }

        private async Task OrchestrationMiddleware(DispatchMiddlewareContext dispatchContext, Func<Task> next)
        {
            TaskOrchestrationShim shim = (TaskOrchestrationShim)dispatchContext.GetProperty<TaskOrchestration>();
            DurableOrchestrationContext context = shim.Context;
            string executorKey = GetFunctionKey(context.Name, context.Version);

            ITriggeredFunctionExecutor functionsPipelineInvoker;
            if (!orchestratorFunctionExecutors.TryGetValue(executorKey, out functionsPipelineInvoker))
            {
                throw new ArgumentException($"No orchestration function named '{context.Name}' with version '{context.Version}' is registered.");
            }

            // 1. Start the functions invocation pipeline (billing, logging, bindings, and timeout tracking).
            FunctionResult result = await functionsPipelineInvoker.TryExecuteAsync(
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
                this.config.TraceHelper.FunctionAwaited(
                    context.HubName,
                    context.Name,
                    context.Version,
                    context.InstanceId,
                    context.IsReplaying);
            }
        }
    }
}
