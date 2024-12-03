// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DurableTask.AzureStorage.Monitoring;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal sealed class DurableTaskScaleMonitor : IScaleMonitor<DurableTaskTriggerMetrics>
    {
        private readonly string hubName;
        private readonly ScaleMonitorDescriptor scaleMonitorDescriptor;
        private readonly ILogger logger;
        private readonly DurableTaskMetricsProvider durableTaskMetricsProvider;

        public DurableTaskScaleMonitor(
            string id,
            string hubName,
            ILogger logger,
            DurableTaskMetricsProvider durableTaskMetricsProvider)
        {
            this.hubName = hubName;
            this.logger = logger;
            this.durableTaskMetricsProvider = durableTaskMetricsProvider;

            // Scalers in Durable Functions are shared for all functions in the same task hub.
            // So instead of using a function ID, we use the task hub name as the basis for the descriptor ID.
            this.scaleMonitorDescriptor = new ScaleMonitorDescriptor(id: id, functionId: id);
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
                    "Durable Functions Trigger Scale Decision for {TaskHub}: {Vote}, Reason: {Reason}",
                    this.hubName,
                    scaleStatus.Vote,
                    scaleRecommendation?.Reason);
            }

            return scaleStatus;
        }
    }
}
