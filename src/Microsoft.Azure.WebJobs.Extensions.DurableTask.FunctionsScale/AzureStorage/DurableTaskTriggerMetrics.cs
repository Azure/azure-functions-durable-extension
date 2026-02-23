// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using Microsoft.Azure.WebJobs.Host.Scale;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.AzureStorage
{
    /// <summary>
    /// Represents scale-related metrics for AzureStorage backend.
    /// Note: This file should remain partially aligned with Microsoft.Azure.WebJobs.Extensions.DurableTask/Listener/DurableTaskTriggerMetrics.
    /// </summary>
    public class DurableTaskTriggerMetrics : ScaleMetrics
    {
        /// <summary>
        /// Gets or sets the number of partitions in the task hub.
        /// </summary>
        public virtual int PartitionCount { get; set; }

        /// <summary>
        /// Gets or sets the number of messages across control queues.
        /// </summary>
        public virtual string ControlQueueLengths { get; set; }

        /// <summary>
        /// Gets or sets the latency of messages across control queues.
        /// </summary>
        public string ControlQueueLatencies { get; set; }

        /// <summary>
        /// Gets or sets the number of messages in the work-item queue.
        /// </summary>
        public virtual int WorkItemQueueLength { get; set; }

        /// <summary>
        /// Gets or sets the approximate age of the first work-item queue message.
        /// </summary>
        public string WorkItemQueueLatency { get; set; }
    }
}
