// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Serializing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// The persisted state of an actor scheduler, as handed forward between ContinueAsNew instances.
    /// </summary>
    internal class SchedulerState
    {
        /// <summary>
        /// Whether this actor exists or not.
        /// </summary>
        [JsonProperty(PropertyName = "exists", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool ActorExists { get; set; }

        /// <summary>
        /// The serialized actor state. This can be stale while CurrentStateView != null.
        /// </summary>
        [JsonProperty(PropertyName = "state", NullValueHandling = NullValueHandling.Ignore)]
        public string ActorState { get; set; }

        /// <summary>
        /// The queue of waiting operations, or null if none.
        /// </summary>
        [JsonProperty(PropertyName = "queue", NullValueHandling = NullValueHandling.Ignore)]
        public Queue<OperationMessage> Queue { get; private set; }

        [JsonIgnore]
        public bool IsEmpty => !ActorExists && (Queue == null || Queue.Count == 0);

        internal IStateView CurrentStateView { get; set; }

        internal void Enqueue(OperationMessage operationMessage)
        {
            if (this.Queue == null)
            {
                this.Queue = new Queue<OperationMessage>();
            }

            this.Queue.Enqueue(operationMessage);
        }

        internal bool TryDequeue(out OperationMessage operationMessage)
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
            return $"state.length={((this.ActorState != null) ? this.ActorState.Length : 0)} queue.count={((this.Queue != null) ? this.Queue.Count : 0)}";
        }
    }
}