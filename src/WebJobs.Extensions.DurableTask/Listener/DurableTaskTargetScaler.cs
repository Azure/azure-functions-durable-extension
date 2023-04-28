// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if FUNCTIONS_V3_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Castle.Components.DictionaryAdapter;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Listener
{
    internal class DurableTaskTargetScaler : ITargetScaler
    {
        private readonly DurableTaskMetricsProvider metricsProvider;
        private readonly TargetScalerResult cachedTargetScaler;
        private readonly DurabilityProvider durabilityProvider;

        public DurableTaskTargetScaler(string functionId, DurableTaskMetricsProvider metricsProvider, DurabilityProvider durabilityProvider)
        {
            this.metricsProvider = metricsProvider;
            this.cachedTargetScaler = new TargetScalerResult();
            this.TargetScalerDescriptor = new TargetScalerDescriptor(functionId);
            this.durabilityProvider = durabilityProvider;
        }

        public TargetScalerDescriptor TargetScalerDescriptor { get; private set; }

        private int MaxConcurrentActivities => this.durabilityProvider.MaxConcurrentTaskActivityWorkItems;

        private int MaxConcurrentOrchestrators => this.durabilityProvider.MaxConcurrentTaskOrchestrationWorkItems;

        public async Task<TargetScalerResult> GetScaleResultAsync(TargetScalerContext context)
        {
            var metrics = await this.metricsProvider.GetMetricsAsync();

            var workItemQueueLength = metrics.WorkItemQueueLength;
            double activityWorkers = Math.Ceiling(workItemQueueLength / (double)this.MaxConcurrentActivities);

            var serializedControlQueueLengths = metrics.ControlQueueLengths;
            var controlQueueLengths = JsonConvert.DeserializeObject<IReadOnlyList<int>>(serializedControlQueueLengths);

            var controlQueueMessages = controlQueueLengths.Sum();
            var activeControlQueues = controlQueueLengths.Count(x => x > 0);

            var upperBoundControlWorkers = Math.Ceiling(controlQueueMessages / (double)this.MaxConcurrentOrchestrators);
            var orchestratorWorkers = Math.Min(activeControlQueues, upperBoundControlWorkers);

            int numWorkersToRequest = (int)Math.Max(activityWorkers, orchestratorWorkers);
            this.cachedTargetScaler.TargetWorkerCount = numWorkersToRequest;
            return this.cachedTargetScaler;
        }
    }
}
#endif