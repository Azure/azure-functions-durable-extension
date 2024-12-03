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
        }

        public virtual async Task<DurableTaskTriggerMetrics> GetMetricsAsync()
        {
            DurableTaskTriggerMetrics metrics = new DurableTaskTriggerMetrics();

            // Durable stores its own metrics, so we just collect them here
            PerformanceHeartbeat heartbeat = null;
            try
            {
                DisconnectedPerformanceMonitor performanceMonitor = this.GetPerformanceMonitor();
                heartbeat = await performanceMonitor.PulseAsync();
            }
            catch (Exception e) when (e.InnerException is RequestFailedException)
            {
                this.logger.LogWarning("{details}. HubName: {hubName}.", e.ToString(), this.hubName);
            }

            if (heartbeat != null)
            {
                metrics.PartitionCount = heartbeat.PartitionCount;
                metrics.ControlQueueLengths = JsonConvert.SerializeObject(heartbeat.ControlQueueLengths);
                metrics.ControlQueueLatencies = JsonConvert.SerializeObject(heartbeat.ControlQueueLatencies);
                metrics.WorkItemQueueLength = heartbeat.WorkItemQueueLength;
                if (heartbeat.WorkItemQueueLatency > TimeSpan.Zero)
                {
                    metrics.WorkItemQueueLatency = heartbeat.WorkItemQueueLatency.ToString();
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