// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Listener;
using Microsoft.Azure.WebJobs.Host.Listeners;
#if !FUNCTIONS_V1
using Microsoft.Azure.WebJobs.Host.Scale;
#endif

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
#if !FUNCTIONS_V1
    internal sealed class DurableTaskListener : IListener, IScaleMonitorProvider, ITargetScalerProvider
#else
    internal sealed class DurableTaskListener : IListener
#endif
    {
        private readonly DurableTaskExtension config;
        private readonly string functionId;
        private readonly FunctionName functionName;
        private readonly FunctionType functionType;
        private readonly string connectionName;
#if !FUNCTIONS_V1
        private readonly Lazy<IScaleMonitor> scaleMonitor;
        private readonly Lazy<ITargetScaler> targetScaler;
#endif

        public DurableTaskListener(
            DurableTaskExtension config,
            string functionId,
            FunctionName functionName,
            FunctionType functionType,
            string connectionName)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));

            if (functionName == default(FunctionName))
            {
                throw new ArgumentNullException(nameof(functionName));
            }

            this.functionId = functionId;
            this.functionName = functionName;
            this.functionType = functionType;
            this.connectionName = connectionName;
#if !FUNCTIONS_V1

            this.scaleMonitor = new Lazy<IScaleMonitor>(() =>
                this.config.GetScaleMonitor(
                    this.functionId,
                    this.functionName,
                    this.connectionName));

            this.targetScaler = new Lazy<ITargetScaler>(() =>
                this.config.GetTargetScaler(
                    this.functionId,
                    this.functionName,
                    this.connectionName));
#endif
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

#if !FUNCTIONS_V1
        public IScaleMonitor GetMonitor()
        {
            return this.scaleMonitor.Value;
        }

        public ITargetScaler GetTargetScaler()
        {
            return this.targetScaler.Value;
        }
#endif
    }
}
