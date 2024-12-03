// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class DurableTaskTargetScaler : ITargetScaler
    {
        private readonly DurableTaskMetricsProvider metricsProvider;
        private readonly TargetScalerResult scaleResult;
        private readonly DurabilityProvider durabilityProvider;
        private readonly ILogger logger;
        private readonly string scaler;

        public DurableTaskTargetScaler(
            string scalerId,
            DurableTaskMetricsProvider metricsProvider,
            DurabilityProvider durabilityProvider,
            ILogger logger)
        {
            this.scaler = scalerId;
            this.metricsProvider = metricsProvider;
            this.scaleResult = new TargetScalerResult();
            this.TargetScalerDescriptor = new TargetScalerDescriptor(this.scaler);
            this.durabilityProvider = durabilityProvider;
            this.logger = logger;
        }

        public TargetScalerDescriptor TargetScalerDescriptor { get; }

        private int MaxConcurrentActivities => this.durabilityProvider.MaxConcurrentTaskActivityWorkItems;

        private int MaxConcurrentOrchestrators => this.durabilityProvider.MaxConcurrentTaskOrchestrationWorkItems;

        public async Task<TargetScalerResult> GetScaleResultAsync(TargetScalerContext context)
        {
            DurableTaskTriggerMetrics? metrics = null;
            try
            {
                // This method is only invoked by the ScaleController, so it doesn't run in the Functions Host process.
                metrics = await this.metricsProvider.GetMetricsAsync();

                // compute activityWorkers: the number of workers we need to process all activity messages
                var workItemQueueLength = metrics.WorkItemQueueLength;
                double activityWorkers = Math.Ceiling(workItemQueueLength / (double)this.MaxConcurrentActivities);

                var serializedControlQueueLengths = metrics.ControlQueueLengths;
                var controlQueueLengths = JsonConvert.DeserializeObject<IReadOnlyList<int>>(serializedControlQueueLengths);

                var controlQueueMessages = controlQueueLengths!.Sum();
                var activeControlQueues = controlQueueLengths!.Count(x => x > 0);

                // compute orchestratorWorkers: the number of workers we need to process all orchestrator messages.
                // We bound this result to be no larger than the partition count
                var upperBoundControlWorkers = Math.Ceiling(controlQueueMessages / (double)this.MaxConcurrentOrchestrators);
                var orchestratorWorkers = Math.Min(activeControlQueues, upperBoundControlWorkers);

                int numWorkersToRequest = (int)Math.Max(activityWorkers, orchestratorWorkers);
                this.scaleResult.TargetWorkerCount = numWorkersToRequest;

                // When running on ScaleController V3, ILogger logs are forwarded to the ScaleController's Kusto table.
                // This works because this code does not execute in the Functions Host process, but in the ScaleController process,
                // and the ScaleController is injecting it's own custom ILogger implementation that forwards logs to Kusto.
                var metricsLog = $"Metrics: workItemQueueLength={workItemQueueLength}. controlQueueLengths={serializedControlQueueLengths}. " +
                    $"maxConcurrentOrchestrators={this.MaxConcurrentOrchestrators}. maxConcurrentActivities={this.MaxConcurrentActivities}";
                var scaleControllerLog = $"Target worker count for '{this.scaler}' is '{numWorkersToRequest}'. " +
                    metricsLog;

                // target worker count should never be negative
                if (numWorkersToRequest < 0)
                {
                    throw new InvalidOperationException("Number of workers to request cannot be negative");
                }

                this.logger.LogInformation(scaleControllerLog);
                return this.scaleResult;
            }
            catch (Exception ex)
            {
                // We want to augment the exception with metrics information for investigation purposes
                var metricsLog = $"Metrics: workItemQueueLength={metrics?.WorkItemQueueLength}. controlQueueLengths={metrics?.ControlQueueLengths}. " +
                    $"maxConcurrentOrchestrators={this.MaxConcurrentOrchestrators}. maxConcurrentActivities={this.MaxConcurrentActivities}";
                var errorLog = $"Error: target worker count for '{this.scaler}' resulted in exception. " + metricsLog;
                throw new Exception(errorLog, ex);
            }
        }
    }
}