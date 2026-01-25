// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using Azure;
using DurableTask.AzureStorage;
using DurableTask.AzureStorage.Monitoring;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class DurableTaskMetricsProvider
    {
        private readonly string hubName;
        private readonly ILogger logger;
        private readonly StorageAccountClientProvider storageAccountClientProvider;
        private PerformanceHeartbeat heartbeat;
        private DateTime heartbeatTimeStamp;

        private DisconnectedPerformanceMonitor performanceMonitor;

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

        public virtual async Task<DurableTaskTriggerMetrics> GetMetricsAsync()
        {
            DurableTaskTriggerMetrics metrics = new DurableTaskTriggerMetrics();

            // Durable stores its own metrics, so we just collect them here
            try
            {
                DisconnectedPerformanceMonitor performanceMonitor = this.GetPerformanceMonitor();

                // We only want to call PulseAsync every 5 seconds
                if (this.heartbeat == null || DateTime.UtcNow > this.heartbeatTimeStamp.AddSeconds(5))
                {
                    this.heartbeat = await performanceMonitor.PulseAsync();
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