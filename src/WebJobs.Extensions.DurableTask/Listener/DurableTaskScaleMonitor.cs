// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if !FUNCTIONS_V1
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DurableTask.AzureStorage.Monitoring;
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
        private readonly CloudStorageAccount storageAccount;
        private readonly ScaleMonitorDescriptor scaleMonitorDescriptor;
        private readonly ILogger logger;
        private readonly DurableTaskMetricsProvider durableTaskMetricsProvider;

        private DisconnectedPerformanceMonitor performanceMonitor;

        public DurableTaskScaleMonitor(
            string functionId,
            string functionName,
            string hubName,
            CloudStorageAccount storageAccount,
            ILogger logger,
            DurableTaskMetricsProvider durableTaskMetricsProvider,
            DisconnectedPerformanceMonitor performanceMonitor = null)
        {
            this.functionId = functionId;
            this.functionName = functionName;
            this.hubName = hubName;
            this.storageAccount = storageAccount;
            this.logger = logger;
            this.performanceMonitor = performanceMonitor;
            this.durableTaskMetricsProvider = durableTaskMetricsProvider;

#if FUNCTIONS_V3_OR_GREATER
            this.scaleMonitorDescriptor = new ScaleMonitorDescriptor($"{this.functionId}-DurableTaskTrigger-{this.hubName}".ToLower(), this.functionId);
#else
#pragma warning disable CS0618 // Type or member is obsolete.

            // We need this because the new ScaleMonitorDescriptor constructor is not compatible with the WebJobs version of Functions V1 and V2.
            // Technically, it is also not available in Functions V3, but we don't have a TFM allowing us to differentiate between Functions V3 and V4.
            this.scaleMonitorDescriptor = new ScaleMonitorDescriptor($"{this.functionId}-DurableTaskTrigger-{this.hubName}".ToLower());
#pragma warning restore CS0618 // Type or member is obsolete. However, the new interface is not compatible with Functions V2 and V1
#endif
        }

        public ScaleMonitorDescriptor Descriptor
        {
            get
            {
                return this.scaleMonitorDescriptor;
            }
        }

        public DurableTaskMetricsProvider GetMetricsProvider()
        {
            return this.durableTaskMetricsProvider;
        }

        async Task<ScaleMetrics> IScaleMonitor.GetMetricsAsync()
        {
            return await this.GetMetricsAsync();
        }

        public async Task<DurableTaskTriggerMetrics> GetMetricsAsync()
        {
            return await this.durableTaskMetricsProvider.GetMetricsAsync();
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

            DisconnectedPerformanceMonitor performanceMonitor = this.durableTaskMetricsProvider.GetPerformanceMonitor();
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