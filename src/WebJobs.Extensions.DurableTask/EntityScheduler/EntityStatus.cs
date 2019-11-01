// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Information about the current status of an entity. Excludes potentially large data
    /// (such as the entity state, or the contents of the queue) so it can always be read with low latency.
    /// </summary>
    public class EntityStatus
    {
        /// <summary>
        /// Whether this entity exists or not.
        /// </summary>
        [JsonProperty(PropertyName = "entityExists", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool EntityExists { get; set; }

        /// <summary>
        /// The size of the queue, i.e. the number of operations that are waiting for the current operation to complete.
        /// </summary>
        [JsonProperty(PropertyName = "queueSize", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int QueueSize { get; set; }

        /// <summary>
        /// The instance id of the orchestration that currently holds the lock of this entity.
        /// </summary>
        [JsonProperty(PropertyName = "lockedBy", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string LockedBy { get; set; }

        /// <summary>
        /// The operation that is currently executing on this entity.
        /// </summary>
        [JsonProperty(PropertyName = "currentOperation", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public EntityCurrentOperationStatus CurrentOperation { get; set; }
    }
}
