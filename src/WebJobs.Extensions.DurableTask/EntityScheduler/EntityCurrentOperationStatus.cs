// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Information about the current status of an operation executing on an entity.
    /// Excludes potentially large data (such as the operation input) so it can be read with low latency.
    /// </summary>
    public class EntityCurrentOperationStatus
    {
        /// <summary>
        /// The name of the operation.
        /// </summary>
        [JsonProperty(PropertyName = "op", Required = Required.Always)]
        public string Operation { get; set; }

        /// <summary>
        /// The unique identifier for this operation.
        /// </summary>
        [JsonProperty(PropertyName = "id", Required = Required.Always)]
        public Guid Id { get; set; }

        /// <summary>
        /// The parent instance that called this operation.
        /// </summary>
        [JsonProperty(PropertyName = "parent", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string ParentInstanceId { get; set; }

        /// <summary>
        /// The UTC time at which the entity started processing this operation.
        /// </summary>
        [JsonProperty(PropertyName = "startTime", Required = Required.Always)]
        public DateTime StartTime { get; set; }
    }
}
