// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Represents the status of a durable orchestration instance.
    /// </summary>
    public class DurableOrchestrationStatus
    {
        /// <summary>
        /// Gets the name of the queried orchestrator function.
        /// </summary>
        /// <value>
        /// The orchestrator function name.
        /// </value>
        public string Name { get; internal set; }

        /// <summary>
        /// Gets the ID of the queried orchestration instance.
        /// </summary>
        /// <remarks>
        /// The instance ID is generated and fixed when the orchestrator function is scheduled. It can be either
        /// auto-generated, in which case it is formatted as a GUID, or it can be user-specified with any format.
        /// </remarks>
        /// <value>
        /// The ID of the queried instance.
        /// </value>
        public string InstanceId { get; internal set; }

        /// <summary>
        /// Gets the time at which the queried orchestration instance was created.
        /// </summary>
        /// <value>
        /// The creation time in UTC.
        /// </value>
        public DateTime CreatedTime { get; internal set; }

        /// <summary>
        /// Gets the time at which the queried orchestration instance last updated its execution history.
        /// </summary>
        /// <value>
        /// The last-updated time in UTC.
        /// </value>
        public DateTime LastUpdatedTime { get; internal set; }

        /// <summary>
        /// Gets the input of the queried orchestrator function instance.
        /// </summary>
        /// <value>
        /// The input as a <c>JToken</c> or <c>null</c> if no input was provided.
        /// </value>
        public JToken Input { get; internal set; }

        /// <summary>
        /// Gets the output of the queried orchestration instance.
        /// </summary>
        /// <value>
        /// The output as a <c>JToken</c> object or <c>null</c> if it has not yet completed.
        /// </value>
        public JToken Output { get; internal set; }

        /// <summary>
        /// Gets the runtime status of the queried orchestration instance.
        /// </summary>
        /// <value>
        /// Expected values include `Running`, `Pending`, `Failed`, `Canceled`, `Terminated`, `Completed`.
        /// </value>
        public OrchestrationRuntimeStatus RuntimeStatus { get; internal set; }

        /// <summary>
        /// Gets a custom status payload that was assigned by the orchestrator function.
        /// </summary>
        /// <value>
        /// The custom status as a <c>JToken</c> object or <c>null</c> if no custom status has been set.
        /// </value>
        public JToken CustomStatus { get; internal set; }

        /// <summary>
        /// Gets the execution history of the queried orchestrator function instance.
        /// </summary>
        /// <value>
        /// The output as a <c>JArray</c> object or <c>null</c>.
        /// </value>
        public JArray History { get; internal set; }
    }
}
