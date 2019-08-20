// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal sealed class DurableTaskListener : IListener
    {
        private readonly DurableTaskExtensionBase config;
        private readonly FunctionName functionName;
        private readonly ITriggeredFunctionExecutor executor;
        private readonly FunctionType functionType;

        public DurableTaskListener(
            DurableTaskExtensionBase config,
            FunctionName functionName,
            ITriggeredFunctionExecutor executor,
            FunctionType functionType)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));

            if (functionName == default(FunctionName))
            {
                throw new ArgumentNullException(nameof(functionName));
            }

            this.functionName = functionName;
            this.functionType = functionType;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return this.config.StartTaskHubWorkerIfNotStartedAsync();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // The actual listener is a task hub worker, which is shared by all orchestration
            // and activity function listeners in the function app. The task hub worker
            // gets shut down only when all durable functions are shut down.
            switch (this.functionType)
            {
                case FunctionType.Orchestrator:
                    this.config.DeregisterOrchestrator(this.functionName);
                    break;
                case FunctionType.Entity:
                    this.config.DeregisterEntity(this.functionName);
                    break;
                case FunctionType.Activity:
                    this.config.DeregisterActivity(this.functionName);
                    break;
            }

            return this.config.StopTaskHubWorkerIfIdleAsync();
        }

        public void Cancel()
        {
            this.StopAsync(CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
        }
    }
}
