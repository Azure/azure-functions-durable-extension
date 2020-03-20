// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// The persisted state of an entity scheduler, as handed forward between ContinueAsNew instances.
    /// </summary>
    internal class SchedulerState
    {
        /// <summary>
        /// Whether this entity exists or not.
        /// </summary>
        [JsonProperty(PropertyName = "exists", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool EntityExists { get; set; }

        /// <summary>
        /// The last serialized entity state.
        /// </summary>
        [JsonProperty(PropertyName = "state", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string EntityState { get; set; }

        /// <summary>
        /// The queue of waiting operations, or null if none.
        /// </summary>
        [JsonProperty(PropertyName = "queue", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Queue<RequestMessage> Queue { get; private set; }

        /// <summary>
        /// The instance id of the orchestration that currently holds the lock of this entity.
        /// </summary>
        [JsonProperty(PropertyName = "lockedBy", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string LockedBy { get; set; }

        /// <summary>
        /// The metadata used for reordering and deduplication of messages sent to entities.
        /// </summary>
        [JsonProperty(PropertyName = "sorter", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public MessageSorter MessageSorter { get; set; } = new MessageSorter();

        [JsonIgnore]
        public bool IsEmpty => !this.EntityExists && (this.Queue == null || this.Queue.Count == 0) && this.LockedBy == null;

        internal void Enqueue(RequestMessage operationMessage)
        {
            if (this.Queue == null)
            {
                this.Queue = new Queue<RequestMessage>();
            }

            this.Queue.Enqueue(operationMessage);
        }

        internal bool TryDequeue(out RequestMessage operationMessage)
        {
            operationMessage = null;

            if (this.Queue == null)
            {
                return false;
            }

            operationMessage = this.Queue.Dequeue();

            if (this.Queue.Count == 0)
            {
                this.Queue = null;
            }

            return true;
        }

        public override string ToString()
        {
            return $"exists={this.EntityExists} queue.count={((this.Queue != null) ? this.Queue.Count : 0)}";
        }
    }
}