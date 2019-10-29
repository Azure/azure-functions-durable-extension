// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#if NETSTANDARD2_0
using System;
using System.Collections.Generic;
using System.Text;
using DurableTask.AzureStorage.Monitoring;
using Microsoft.Azure.WebJobs.Host.Scale;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class DurableTaskTriggerMetrics : ScaleMetrics
    {
        /// <summary>
        /// The number of partitions in the task hub.
        /// </summary>
        public int PartitionCount { get; set; }

        /// <summary>
        /// The number of messages across control queues. This will
        /// be in the form of a serialized array of ints, e.g. "[1,2,3,4]".
        /// </summary>
        public string ControlQueueLengths { get; set; }

        /// <summary>
        /// The latency of messages across control queues. This will
        /// be in the form of a serialized array of TimeSpans in string
        /// format, e.g. "["00:00:00.0010000","00:00:00.0020000","00:00:00.0030000","00:00:00.0040000"]".
        /// </summary>
        public string ControlQueueLatencies { get; set; }

        /// <summary>
        /// The number of messages in the work-item queue.
        /// </summary>
        public int WorkItemQueueLength { get; set; }

        /// <summary>
        /// The approximate age of the first work-item queue message. This
        /// will be a TimeSpan in string format, e.g. "00:00:00.0010000".
        /// </summary>
        public string WorkItemQueueLatency { get; set; }
    }
}
#endif
