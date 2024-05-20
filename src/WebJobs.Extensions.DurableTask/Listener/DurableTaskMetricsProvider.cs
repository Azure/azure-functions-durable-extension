// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if !FUNCTIONS_V1
using System;
using System.Threading.Tasks;
<<<<<<< HEAD
using DurableTask.AzureStorage.Monitoring;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
=======
using Azure;
using DurableTask.AzureStorage;
using DurableTask.AzureStorage.Monitoring;
using Microsoft.Extensions.Logging;
>>>>>>> v3.x
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class DurableTaskMetricsProvider
    {
        private readonly string functionName;
        private readonly string hubName;
        private readonly ILogger logger;
<<<<<<< HEAD
        private readonly CloudStorageAccount storageAccount;

        private DisconnectedPerformanceMonitor performanceMonitor;

        public DurableTaskMetricsProvider(string functionName, string hubName, ILogger logger, DisconnectedPerformanceMonitor performanceMonitor, CloudStorageAccount storageAccount)
=======
        private readonly StorageAccountClientProvider storageAccountClientProvider;

        private DisconnectedPerformanceMonitor performanceMonitor;

        public DurableTaskMetricsProvider(string functionName, string hubName, ILogger logger, DisconnectedPerformanceMonitor performanceMonitor, StorageAccountClientProvider storageAccountClientProvider)
>>>>>>> v3.x
        {
            this.functionName = functionName;
            this.hubName = hubName;
            this.logger = logger;
            this.performanceMonitor = performanceMonitor;
<<<<<<< HEAD
            this.storageAccount = storageAccount;
=======
            this.storageAccountClientProvider = storageAccountClientProvider;
>>>>>>> v3.x
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
<<<<<<< HEAD
            catch (StorageException e)
=======
            catch (Exception e) when (e.InnerException is RequestFailedException)
>>>>>>> v3.x
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

        internal DisconnectedPerformanceMonitor GetPerformanceMonitor()
        {
            if (this.performanceMonitor == null)
            {
<<<<<<< HEAD
                if (this.storageAccount == null)
                {
                    throw new ArgumentNullException(nameof(this.storageAccount));
                }

                this.performanceMonitor = new DisconnectedPerformanceMonitor(this.storageAccount, this.hubName);
=======
                if (this.storageAccountClientProvider == null)
                {
                    throw new ArgumentNullException(nameof(this.storageAccountClientProvider));
                }

                this.performanceMonitor = new DisconnectedPerformanceMonitor(new AzureStorageOrchestrationServiceSettings
                {
                    StorageAccountClientProvider = this.storageAccountClientProvider,
                    TaskHubName = this.hubName,
                });
>>>>>>> v3.x
            }

            return this.performanceMonitor;
        }
    }
}
#endif