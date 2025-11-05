// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.DurableTask.AzureManagedBackend;
using Microsoft.DurableTask.AzureManagedBackend.Metrics;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.AzureManaged
{
    internal class AzureManagedTargetScaler : ITargetScaler
    {
        private readonly AzureManagedOrchestrationService service;
        private readonly TargetScalerDescriptor descriptor;

        public AzureManagedTargetScaler(AzureManagedOrchestrationService service, string functionId)
        {
            this.service = service;
            this.descriptor = new TargetScalerDescriptor(functionId);
        }

        public TargetScalerDescriptor TargetScalerDescriptor => this.descriptor;

        public async Task<TargetScalerResult> GetScaleResultAsync(TargetScalerContext context)
        {
            TaskHubMetrics metrics = await this.service.GetTaskHubMetricsAsync(default);
            if (metrics is null)
            {
                return new TargetScalerResult { TargetWorkerCount = 0 };
            }

            static int GetTargetWorkerCount(WorkItemMetrics workItemMetrics, int workItemCapacity)
            {
                if (workItemCapacity == 0)
                {
                    return 0;
                }

                int totalWorkItemCount = workItemMetrics.PendingCount + workItemMetrics.ActiveCount;
                return (int)Math.Ceiling((double)totalWorkItemCount / workItemCapacity);
            }

            int targetForOrchestratorWorkItems = GetTargetWorkerCount(
                workItemMetrics: metrics.OrchestratorWorkItems,
                workItemCapacity: this.service.MaxConcurrentTaskOrchestrationWorkItems);

            int targetForActivityWorkItems = GetTargetWorkerCount(
                workItemMetrics: metrics.ActivityWorkItems,
                workItemCapacity: this.service.MaxConcurrentTaskActivityWorkItems);

            int targetForEntityWorkItems = GetTargetWorkerCount(
                workItemMetrics: metrics.EntityWorkItems,
                workItemCapacity: this.service.MaxConcurrentEntityWorkItems);

            // Scale out to the maximum of the above target values.
            int maxTarget = Math.Max(
                targetForOrchestratorWorkItems,
                Math.Max(targetForActivityWorkItems, targetForEntityWorkItems));

            return new TargetScalerResult { TargetWorkerCount = maxTarget };
        }
    }
}
