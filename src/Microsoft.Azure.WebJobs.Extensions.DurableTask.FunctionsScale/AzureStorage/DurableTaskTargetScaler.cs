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

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.AzureStorage
{
    /// <summary>
    /// Target scaler that computes the desired worker count based on scale metrics from the Azure Storage backend.
    /// Note: This file should remain partially aligned with Microsoft.Azure.WebJobs.Extensions.DurableTask/Listener/DurableTaskTargetScaler.
    /// </summary>
    public class DurableTaskTargetScaler : ITargetScaler
    {
        private readonly DurableTaskMetricsProvider metricsProvider;
        private readonly ScalabilityProvider scalabilityProvider;
        private readonly ILogger logger;
        private readonly string scaler;

        /// <summary>
        /// Initializes a new instance of the <see cref="DurableTaskTargetScaler"/> class.
        /// </summary>
        /// <param name="scalerId">
        /// The unique identifier for this target scaler.
        /// </param>
        /// <param name="metricsProvider">
        /// Provides Durable Task scale metrics used to compute the target worker count.
        /// </param>
        /// <param name="scalabilityProvider">
        /// Provides backend-specific scaling capabilities and configuration.
        /// </param>
        /// <param name="logger">
        /// The logger instance used for diagnostics and telemetry.
        /// </param>
        public DurableTaskTargetScaler(
            string scalerId,
            DurableTaskMetricsProvider metricsProvider,
            ScalabilityProvider scalabilityProvider,
            ILogger logger)
        {
            this.scaler = scalerId;
            this.metricsProvider = metricsProvider;
            this.TargetScalerDescriptor = new TargetScalerDescriptor(this.scaler);
            this.scalabilityProvider = scalabilityProvider;
            this.logger = logger;
        }

        /// <summary>
        /// Gets the descriptor that identifies this target scaler.
        /// </summary>
        public TargetScalerDescriptor TargetScalerDescriptor { get; }

        private int MaxConcurrentActivities => this.scalabilityProvider.MaxConcurrentTaskActivityWorkItems;

        private int MaxConcurrentOrchestrators => this.scalabilityProvider.MaxConcurrentTaskOrchestrationWorkItems;

        /// <summary>
        /// Computes the target worker count based on current Durable Task trigger metrics.
        /// </summary>
        /// <param name="context">
        /// The scaling context provided by the scale controller.
        /// </param>
        /// <returns>
        /// A <see cref="TargetScalerResult"/> containing the computed target worker count.
        /// </returns>
        /// <exception cref="Exception">
        /// Thrown when metrics cannot be retrieved or processed.
        /// </exception>
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

                IReadOnlyList<int>? controlQueueLengths = null;
                if (!string.IsNullOrEmpty(serializedControlQueueLengths))
                {
                    controlQueueLengths = JsonConvert.DeserializeObject<IReadOnlyList<int>>(serializedControlQueueLengths);
                }

                var controlQueueMessages = controlQueueLengths?.Sum() ?? 0;
                var activeControlQueues = controlQueueLengths?.Count(x => x > 0) ?? 0;

                // compute orchestratorWorkers: the number of workers we need to process all orchestrator messages.
                // We bound this result to be no larger than the partition count
                var upperBoundControlWorkers = Math.Ceiling(controlQueueMessages / (double)this.MaxConcurrentOrchestrators);
                var orchestratorWorkers = Math.Min(activeControlQueues, upperBoundControlWorkers);

                int numWorkersToRequest = (int)Math.Max(activityWorkers, orchestratorWorkers);

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
                return new TargetScalerResult { TargetWorkerCount = numWorkersToRequest };
            }
            catch (Exception ex)
            {
                // We want to augment the exception with metrics information for investigation purposes
                var metricsLog = $"Metrics: workItemQueueLength={metrics?.WorkItemQueueLength}. controlQueueLengths={metrics?.ControlQueueLengths}. " +
                    $"maxConcurrentOrchestrators={this.MaxConcurrentOrchestrators}. maxConcurrentActivities={this.MaxConcurrentActivities}";
                var errorLog = $"Error: target worker count for '{this.scaler}' resulted in exception. " + metricsLog;

                // Log the enriched error message with the original exception, then rethrow to preserve exception type.
                this.logger.LogError(ex, errorLog);

                throw;
            }
        }
    }
}