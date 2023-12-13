// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Runtime.Serialization;
using DurableTask.Core.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Represents the status of a durable entity instance.
    /// </summary>
    [DataContract]
    public class DurableEntityStatus
    {
        internal DurableEntityStatus() { }

        internal DurableEntityStatus(DurableOrchestrationStatus orchestrationStatus)
        {
            this.EntityId = EntityId.GetEntityIdFromSchedulerId(orchestrationStatus.InstanceId);
            this.LastOperationTime = orchestrationStatus.LastUpdatedTime;

            if (orchestrationStatus?.Input is JObject input)
            {
                SchedulerState state = input.ToObject<SchedulerState>();
                if (state?.EntityState != null)
                {
                    try
                    {
                        // Entity state is expected to be JSON-compatible
                        this.State = JToken.Parse(state.EntityState);
                    }
                    catch (JsonException)
                    {
                        // Just in case the above assumption is ever wrong, fallback to a raw string
                        this.State = state.EntityState;
                    }
                }
            }
        }

        internal DurableEntityStatus(EntityBackendQueries.EntityMetadata metadata)
        {
            this.EntityId = new EntityId(metadata.EntityId.Name, metadata.EntityId.Key);
            this.LastOperationTime = metadata.LastModifiedTime;
            if (metadata.SerializedState != null)
            {
                try
                {
                    // Entity state is expected to be JSON-compatible
                    this.State = JToken.Parse(metadata.SerializedState);
                }
                catch (JsonException)
                {
                    // Just in case the above assumption is ever wrong, fallback to a raw string
                    this.State = metadata.SerializedState;
                }
            }
        }

        /// <summary>
        /// Gets the EntityId of the queried entity instance.
        /// </summary>
        /// <value>
        /// The unique EntityId of the instance.
        /// </value>
        [DataMember(Name = "entityId")]
        public EntityId EntityId { get; set; }

        /// <summary>
        /// Gets the time of the last operation processed by the entity instance.
        /// </summary>
        /// <value>
        /// The last operation time in UTC.
        /// </value>
        [DataMember(Name = "lastOperationTime")]
        public DateTime LastOperationTime { get; set; }

        /// <summary>
        /// Gets the state of the entity instance.
        /// </summary>
        /// <value>
        /// The state as either a <c>JToken</c> or <c>null</c> if no state was provided.
        /// </value>
        [DataMember(Name = "state")]
        public JToken State { get; set; }
    }
}
