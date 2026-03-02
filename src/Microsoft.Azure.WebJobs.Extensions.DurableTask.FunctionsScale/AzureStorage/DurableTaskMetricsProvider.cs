// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using Azure;
using DurableTask.AzureStorage;
using DurableTask.AzureStorage.Monitoring;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.AzureStorage
{
    /// <summary>
    /// Collects Durable Task scale metrics from the Azure Storage backend.
    /// </summary>
    public class DurableTaskMetricsProvider
    {
        private readonly string hubName;
        private readonly ILogger logger;
        private readonly StorageAccountClientProvider storageAccountClientProvider;
        private PerformanceHeartbeat heartbeat;
        private DateTime heartbeatTimeStamp;

        private DisconnectedPerformanceMonitor performanceMonitor;

        /// <summary>
        /// Initializes a new instance of the <see cref="DurableTaskMetricsProvider"/> class.
        /// </summary>
        /// <param name="hubName">
        /// The name of the task hub from which metrics are collected.
        /// </param>
        /// <param name="logger">
        /// The logger used for diagnostic and warning messages.
        /// </param>
        /// <param name="performanceMonitor">
        /// The performance monitor used to retrieve task hub heartbeat data.
        /// </param>
        /// <param name="storageAccountClientProvider">
        /// Provides Azure Storage clients required to access task hub resources.
        /// </param>
        /// Note: This file should remain partially aligned with Microsoft.Azure.WebJobs.Extensions.DurableTask/Listener/DurableTaskMetricsProvider.
        public DurableTaskMetricsProvider(
            string hubName,
            ILogger logger,
            DisconnectedPerformanceMonitor performanceMonitor,
            StorageAccountClientProvider storageAccountClientProvider)
        {
            this.hubName = hubName;
            this.logger = logger;
            this.performanceMonitor = performanceMonitor;
            this.storageAccountClientProvider = storageAccountClientProvider;
            this.heartbeat = null;
            this.heartbeatTimeStamp = DateTime.MinValue;
        }

        /// <summary>
        /// Get current metrics on Azure Storage backend.
        /// </summary>
        /// <returns>DurableTaskTriggerMetrics with target worker count.</returns>
        public virtual async Task<DurableTaskTriggerMetrics> GetMetricsAsync()
        {
            DurableTaskTriggerMetrics metrics = new DurableTaskTriggerMetrics();

            // Durable stores its own metrics, so we just collect them here
            try
            {
                DisconnectedPerformanceMonitor currentPerformanceMonitor = this.GetPerformanceMonitor();

                // We only want to call PulseAsync every 5 seconds
                if (this.heartbeat == null || DateTime.UtcNow > this.heartbeatTimeStamp.AddSeconds(5))
                {
                    this.heartbeat = await currentPerformanceMonitor.PulseAsync();
                    this.heartbeatTimeStamp = DateTime.UtcNow;
                }
            }
            catch (Exception e) when (e.InnerException is RequestFailedException)
            {
                this.logger.LogWarning("{details}. HubName: {hubName}.", e.ToString(), this.hubName);
            }

            if (this.heartbeat != null)
            {
                metrics.PartitionCount = this.heartbeat.PartitionCount;
                metrics.ControlQueueLengths = JsonConvert.SerializeObject(this.heartbeat.ControlQueueLengths);
                metrics.ControlQueueLatencies = JsonConvert.SerializeObject(this.heartbeat.ControlQueueLatencies);
                metrics.WorkItemQueueLength = this.heartbeat.WorkItemQueueLength;
                if (this.heartbeat.WorkItemQueueLatency > TimeSpan.Zero)
                {
                    metrics.WorkItemQueueLatency = this.heartbeat.WorkItemQueueLatency.ToString();
                }
            }

            return metrics;
        }

        internal DisconnectedPerformanceMonitor GetPerformanceMonitor()
        {
            if (this.performanceMonitor == null)
            {
                if (this.storageAccountClientProvider == null)
                {
                    throw new ArgumentNullException(nameof(this.storageAccountClientProvider));
                }

                this.performanceMonitor = new DisconnectedPerformanceMonitor(new AzureStorageOrchestrationServiceSettings
                {
                    StorageAccountClientProvider = this.storageAccountClientProvider,
                    TaskHubName = this.hubName,
                });
            }

            return this.performanceMonitor;
        }
    }
}
