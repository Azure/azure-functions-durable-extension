// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using DurableTask.Netherite.Scaling;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;
using static DurableTask.Netherite.Scaling.ScalingMonitor;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Netherite
{
    /// <summary>
    /// Target-based scaler for Netherite backend.
    /// Provides target worker count recommendations based on Netherite orchestration service metrics.
    /// This class is in sync with NetheriteTargetScaler at DurableTask.Netherite.AzureFunctions.
    /// </summary>
    public class NetheriteTargetScaler : ITargetScaler
    {
        private readonly NetheriteMetricsProvider metricsProvider;
        private readonly ScalabilityProvider scalabilityProvider;
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetheriteTargetScaler"/> class.
        /// </summary>
        /// <param name="functionId">The ID of the function to scale.</param>
        /// <param name="metricsProvider">The Netherite metrics provider.</param>
        /// <param name="scalabilityProvider">The scalability provider for concurrency limits.</param>
        /// <param name="logger">The logger for diagnostic output.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="metricsProvider"/>, <paramref name="scalabilityProvider"/>,
        /// or <paramref name="logger"/> is null.
        /// </exception>
        public NetheriteTargetScaler(
            string functionId,
            NetheriteMetricsProvider metricsProvider,
            ScalabilityProvider scalabilityProvider,
            ILogger logger)
        {
            this.metricsProvider = metricsProvider ?? throw new ArgumentNullException(nameof(metricsProvider));
            this.scalabilityProvider = scalabilityProvider ?? throw new ArgumentNullException(nameof(scalabilityProvider));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Scalers in Durable Functions are per function IDs. Scalers share the same metricsProvider in the same task hub.
            this.TargetScalerDescriptor = new TargetScalerDescriptor(functionId);
        }

        /// <summary>
        /// Gets the descriptor for this target scaler.
        /// </summary>
        public TargetScalerDescriptor TargetScalerDescriptor { get; }

        /// <summary>
        /// Retrieves the current scale result based on Netherite metrics, returning the recommended number of workers for the task hub.
        /// </summary>
        /// <param name="context">The context for scaling evaluation.</param>
        /// <returns>The calculated <see cref="TargetScalerResult"/>.</returns>
        public async Task<TargetScalerResult> GetScaleResultAsync(TargetScalerContext context)
        {
            Metrics metrics = await this.metricsProvider.GetMetricsAsync();

            int maxConcurrentActivities = this.scalabilityProvider.MaxConcurrentTaskActivityWorkItems;
            int maxConcurrentWorkItems = this.scalabilityProvider.MaxConcurrentTaskOrchestrationWorkItems;

            int target;

            if (string.IsNullOrEmpty(metrics.Busy))
            {
                // Task hub is idle
                this.logger?.LogInformation("Netherite target scaler: Task hub is idle. Recommending 0 workers.");
                return new TargetScalerResult { TargetWorkerCount = 0 };
            }

            // Always need at least one worker when we are not idle
            target = 1;

            // If there is a backlog of activities, ask for enough workers to process them
            int activities = metrics.LoadInformation.Where(info => info.Value.IsLoaded()).Sum(info => info.Value.Activities);
            if (activities > 0)
            {
                int requestedWorkers = (activities + (maxConcurrentActivities - 1)) / maxConcurrentActivities; // rounded-up integer division
                requestedWorkers = Math.Min(requestedWorkers, metrics.LoadInformation.Count); // cannot use more workers than partitions
                target = Math.Max(target, requestedWorkers);
            }

            // If there are load-challenged partitions, ask for a worker for each of them
            int numberOfChallengedPartitions = metrics.LoadInformation.Values
                .Count(info => info.IsLoaded() || info.WorkItems > maxConcurrentWorkItems);
            target = Math.Max(target, numberOfChallengedPartitions);

            // Determine how many different workers are currently running
            int current = metrics.LoadInformation.Values.Select(info => info.WorkerId).Distinct().Count();

            if (target < current)
            {
                // The target is lower than our current scale. However, before
                // scaling in, we check some more things to avoid
                // over-aggressive scale-in that could impact performance negatively.
                int numberOfNonIdlePartitions = metrics.LoadInformation.Values.Count(info => !PartitionLoadInfo.IsLongIdle(info.LatencyTrend));
                if (current > numberOfNonIdlePartitions)
                {
                    // If we have more workers than non-idle partitions, don't immediately go lower than
                    // the number of non-idle partitions.
                    target = Math.Max(target, numberOfNonIdlePartitions);
                }
                else
                {
                    // All partitions are busy, so we don't want to reduce the worker count unless load is very low.
                    // Even if all partitions are running efficiently, it can be hard to know whether it is wise to reduce the worker count.
                    // We want to avoid scaling in unnecessarily when we've reached optimal scale-out.
                    // But we also want to avoid the case where a constant trickle of load after a big scale-out prevents scaling back in.
                    // To balance these goals, we vote to scale down only by one worker at a time when we see this situation.
                    bool allPartitionsAreFast = !metrics.LoadInformation.Values.Any(info =>
                              info.LatencyTrend.Length != PartitionLoadInfo.LatencyTrendLength
                           || info.LatencyTrend.Any(c => c == PartitionLoadInfo.MediumLatency || c == PartitionLoadInfo.HighLatency));

                    if (allPartitionsAreFast)
                    {
                        // Don't go lower than 1 below current
                        target = Math.Max(target, current - 1);
                    }
                    else
                    {
                        // Don't go lower than current
                        target = Math.Max(target, current);
                    }
                }
            }

            this.logger?.LogInformation(
                "Netherite target scaler: Recommending {TargetWorkerCount} workers. Current: {CurrentWorkers}, Activities: {Activities}, Challenged partitions: {ChallengedPartitions}",
                target,
                current,
                activities,
                numberOfChallengedPartitions);

            return new TargetScalerResult { TargetWorkerCount = target };
        }
    }
}
