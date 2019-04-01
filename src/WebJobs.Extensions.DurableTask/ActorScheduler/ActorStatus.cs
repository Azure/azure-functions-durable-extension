// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions
{
    /// <summary>
    /// Information about the current status of an actor. Excludes potentially large data
    /// (such as the actor state, or the contents of the queue) so it can always be read with low latency.
    /// </summary>
    public class ActorStatus
    {
        /// <summary>
        /// Whether this actor exists or not.
        /// </summary>
        [JsonProperty(PropertyName = "actorExists", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool ActorExists { get; set; }

        /// <summary>
        /// The size of the queue, i.e. the number of operations that are waiting for the current operation to complete.
        /// </summary>
        [JsonProperty(PropertyName = "queueSize", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int QueueSize { get; set; }

        /// <summary>
        /// The instance id of the orchestration that currently holds the lock of this actor.
        /// </summary>
        [JsonProperty(PropertyName = "lockedBy", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string LockedBy { get; set; }

        /// <summary>
        /// The operation that is currently executing on this actor.
        /// </summary>
        [JsonProperty(PropertyName = "currentOperation", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public ActorCurrentOperationStatus CurrentOperation { get; set; }
    }
}
