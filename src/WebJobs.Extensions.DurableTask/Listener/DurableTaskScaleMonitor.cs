// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if !FUNCTIONS_V1
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using DurableTask.AzureStorage.Monitoring;
using Dynamitey.DynamicObjects;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal sealed class DurableTaskScaleMonitor : IScaleMonitor<DurableTaskTriggerMetrics>
    {
        private readonly string functionId;
        private readonly string functionName;
        private readonly string hubName;
        private readonly string storageConnectionString;
        private readonly ScaleMonitorDescriptor scaleMonitorDescriptor;
        private readonly ILogger logger;

        private DisconnectedPerformanceMonitor performanceMonitor;

        public DurableTaskScaleMonitor(
            string functionId,
            string functionName,
            string hubName,
            string storageConnectionString,
            ILogger logger,
            DisconnectedPerformanceMonitor performanceMonitor = null)
        {
            this.functionId = functionId;
            this.functionName = functionName;
            this.hubName = hubName;
            this.storageConnectionString = storageConnectionString;
            this.logger = logger;
            this.performanceMonitor = performanceMonitor;
            this.scaleMonitorDescriptor = new ScaleMonitorDescriptor($"{this.functionId}-DurableTaskTrigger-{this.hubName}".ToLower());
        }

        public ScaleMonitorDescriptor Descriptor
        {
            get
            {
                return this.scaleMonitorDescriptor;
            }
        }

        private DisconnectedPerformanceMonitor GetPerformanceMonitor()
        {
            if (this.performanceMonitor == null)
            {
                if (this.storageConnectionString == null)
                {
                    throw new ArgumentNullException(nameof(this.storageConnectionString));
                }

                this.performanceMonitor = new DisconnectedPerformanceMonitor(this.storageConnectionString, this.hubName);
            }

            return this.performanceMonitor;
        }

        async Task<ScaleMetrics> IScaleMonitor.GetMetricsAsync()
        {
            return await this.GetMetricsAsync();
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
                if (heartbeat.WorkItemQueueLatency != null)
                {
                    metrics.WorkItemQueueLatency = heartbeat.WorkItemQueueLatency.ToString();
                }
            }

            return metrics;
        }

        ScaleStatus IScaleMonitor.GetScaleStatus(ScaleStatusContext context)
        {
            return this.GetScaleStatusCore(context.WorkerCount, context.Metrics?.Cast<DurableTaskTriggerMetrics>().ToArray());
        }

        public ScaleStatus GetScaleStatus(ScaleStatusContext<DurableTaskTriggerMetrics> context)
        {
            return this.GetScaleStatusCore(context.WorkerCount, context.Metrics?.ToArray());
        }

        private ScaleStatus GetScaleStatusCore(int workerCount, DurableTaskTriggerMetrics[] metrics)
        {
            var scaleStatus = new ScaleStatus() { Vote = ScaleVote.None };
            if (metrics == null)
            {
                return scaleStatus;
            }

            var heartbeats = new PerformanceHeartbeat[metrics.Length];
            for (int i = 0; i < metrics.Length; ++i)
            {
                TimeSpan workItemQueueLatency;
                bool parseResult = TimeSpan.TryParse(metrics[i].WorkItemQueueLatency, out workItemQueueLatency);

                heartbeats[i] = new PerformanceHeartbeat()
                {
                    PartitionCount = metrics[i].PartitionCount,
                    WorkItemQueueLatency = parseResult ? workItemQueueLatency : TimeSpan.FromMilliseconds(0),
                    WorkItemQueueLength = metrics[i].WorkItemQueueLength,
                };

                if (metrics[i].ControlQueueLengths == null)
                {
                    heartbeats[i].ControlQueueLengths = new List<int>();
                }
                else
                {
                    heartbeats[i].ControlQueueLengths = JsonConvert.DeserializeObject<IReadOnlyList<int>>(metrics[i].ControlQueueLengths);
                }

                if (metrics[i].ControlQueueLatencies == null)
                {
                    heartbeats[i].ControlQueueLatencies = new List<TimeSpan>();
                }
                else
                {
                    heartbeats[i].ControlQueueLatencies = JsonConvert.DeserializeObject<IReadOnlyList<TimeSpan>>(metrics[i].ControlQueueLatencies);
                }
            }

            DisconnectedPerformanceMonitor performanceMonitor = this.GetPerformanceMonitor();
            var scaleRecommendation = performanceMonitor.MakeScaleRecommendation(workerCount, heartbeats.ToArray());

            bool writeToUserLogs = false;
            switch (scaleRecommendation?.Action)
            {
                case ScaleAction.AddWorker:
                    scaleStatus.Vote = ScaleVote.ScaleOut;
                    writeToUserLogs = true;
                    break;
                case ScaleAction.RemoveWorker:
                    scaleStatus.Vote = ScaleVote.ScaleIn;
                    writeToUserLogs = true;
                    break;
                default:
                    scaleStatus.Vote = ScaleVote.None;
                    break;
            }

            if (writeToUserLogs)
            {
                this.logger.LogInformation(
                    $"Durable Functions Trigger Scale Decision: {scaleStatus.Vote.ToString()}, Reason: {scaleRecommendation?.Reason}",
                    this.hubName,
                    this.functionName);
            }

            return scaleStatus;
        }
    }
}
#endif