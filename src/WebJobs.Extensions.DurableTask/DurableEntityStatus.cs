// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Runtime.Serialization;
using DurableTask.Core.Entities;
using DurableTask.Core.Entities.StateFormat;
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
