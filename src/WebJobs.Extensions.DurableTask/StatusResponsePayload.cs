// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Runtime.Serialization;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Response for Orchestration Status Query.
    /// </summary>
    [DataContract]
    internal class StatusResponsePayload
    {
        /// <summary>
        /// InstanceId.
        /// </summary>
        [DataMember(Name = "instanceId")]
        public string InstanceId { get; set; }

        /// <summary>
        /// Runtime status.
        /// </summary>
        [DataMember(Name = "runtimeStatus")]
        public string RuntimeStatus { get; set; }

        /// <summary>
        /// Input.
        /// </summary>
        [DataMember(Name = "input")]
        public JToken Input { get; set; }

        /// <summary>
        /// Custom status.
        /// </summary>
        [DataMember(Name = "customStatus")]
        public JToken CustomStatus { get; set; }

        /// <summary>
        /// Output.
        /// </summary>
        [DataMember(Name = "output")]
        public JToken Output { get; set; }

        /// <summary>
        /// Created time value.
        /// </summary>
        [DataMember(Name = "createdTime")]
        public string CreatedTime { get; set; }

        /// <summary>
        /// Last updated time.
        /// </summary>
        [DataMember(Name = "lastUpdatedTime")]
        public string LastUpdatedTime { get; set; }

        /// <summary>
        /// JSON object representing history for an orchestration execution.
        /// </summary>
        [DataMember(Name = "historyEvents", EmitDefaultValue = false)]
        public JArray HistoryEvents { get; set; }
    }
}
