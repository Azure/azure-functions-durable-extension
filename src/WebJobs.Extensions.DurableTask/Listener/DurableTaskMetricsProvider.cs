// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if !FUNCTIONS_V1
using System;
using System.Threading.Tasks;
using DurableTask.AzureStorage.Monitoring;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Listener
{
    internal class DurableTaskMetricsProvider
    {
        private readonly string functionName;
        private readonly string hubName;
        private readonly ILogger logger;
        private readonly CloudStorageAccount storageAccount;

        private DisconnectedPerformanceMonitor performanceMonitor;

        public DurableTaskMetricsProvider(string functionName, string hubName, ILogger logger, DisconnectedPerformanceMonitor performanceMonitor, CloudStorageAccount storageAccount)
        {
            this.functionName = functionName;
            this.hubName = hubName;
            this.logger = logger;
            this.performanceMonitor = performanceMonitor;
            this.storageAccount = storageAccount;
        }

        public async Task<DurableTaskTriggerMetrics> GetMetricsAsync()
        {
            DurableTaskTriggerMetrics metrics = new DurableTaskTriggerMetrics();

            // Durable stores its own metrics, so we just collect them here
            PerformanceHeartbeat heartbeat = null;
            try
            {
                DisconnectedPerformanceMonitor performanceMonitor = this.GetPerformanceMonitor();
                heartbeat = await performanceMonitor.PulseAsync();
            }
            catch (StorageException e)
            {
                this.logger.LogWarning("{details}. Function: {functionName}. HubName: {hubName}.", e.ToString(), this.functionName, this.hubName);
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

        private DisconnectedPerformanceMonitor GetPerformanceMonitor()
        {
            if (this.performanceMonitor == null)
            {
                if (this.storageAccount == null)
                {
                    throw new ArgumentNullException(nameof(this.storageAccount));
                }

                this.performanceMonitor = new DisconnectedPerformanceMonitor(this.storageAccount, this.hubName);
            }

            return this.performanceMonitor;
        }
    }
}
#endif