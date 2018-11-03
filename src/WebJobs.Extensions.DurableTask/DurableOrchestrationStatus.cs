// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Represents the status of a durable orchestration instance.
    /// </summary>
    /// <remarks>
    /// An external client can fetch the status of an orchestration instance using
    /// <see cref="DurableOrchestrationClient.GetStatusAsync(string, bool, bool, bool)"/>.
    /// </remarks>
    public class DurableOrchestrationStatus
    {
        /// <summary>
        /// Gets the name of the queried orchestrator function.
        /// </summary>
        /// <value>
        /// The orchestrator function name.
        /// </value>
        public string Name { get; set; }

        /// <summary>
        /// Gets the ID of the queried orchestration instance.
        /// </summary>
        /// <remarks>
        /// The instance ID is generated and fixed when the orchestrator function is scheduled. It can be either
        /// auto-generated, in which case it is formatted as a GUID, or it can be user-specified with any format.
        /// </remarks>
        /// <value>
        /// The unique ID of the instance.
        /// </value>
        public string InstanceId { get; set; }

        /// <summary>
        /// Gets the time at which the orchestration instance was created.
        /// </summary>
        /// <remarks>
        /// If the orchestration instance is in the <see cref="OrchestrationRuntimeStatus.Pending"/>
        /// status, this time represents the time at which the orchestration instance was scheduled.
        /// </remarks>
        /// <value>
        /// The instance creation time in UTC.
        /// </value>
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// Gets the time at which the orchestration instance last updated its execution history.
        /// </summary>
        /// <value>
        /// The last-updated time in UTC.
        /// </value>
        public DateTime LastUpdatedTime { get; set; }

        /// <summary>
        /// Gets the input of the orchestrator function instance.
        /// </summary>
        /// <value>
        /// The input as either a <c>JToken</c> or <c>null</c> if no input was provided.
        /// </value>
        public JToken Input { get; set; }

        /// <summary>
        /// Gets the output of the queried orchestration instance.
        /// </summary>
        /// <value>
        /// The output as either a <c>JToken</c> object or <c>null</c> if it has not yet completed.
        /// </value>
        public JToken Output { get; set; }

        /// <summary>
        /// Gets the runtime status of the queried orchestration instance.
        /// </summary>
        /// <value>
        /// Expected values include `Running`, `Pending`, `Failed`, `Canceled`, `Terminated`, `Completed`.
        /// </value>
        public OrchestrationRuntimeStatus RuntimeStatus { get; set; }

        /// <summary>
        /// Gets the custom status payload (if any) that was set by the orchestrator function.
        /// </summary>
        /// <remarks>
        /// Orchestrator functions can set a custom status using <see cref="DurableOrchestrationContext.SetCustomStatus"/>.
        /// </remarks>
        /// <value>
        /// The custom status as either a <c>JToken</c> object or <c>null</c> if no custom status has been set.
        /// </value>
        public JToken CustomStatus { get; set; }

        /// <summary>
        /// Gets the execution history of the orchestration instance.
        /// </summary>
        /// <remarks>
        /// The history log can be large and is therefore <c>null</c> by default.
        /// It is populated only when explicitly requested in the call to
        /// <see cref="DurableOrchestrationClient.GetStatusAsync(string, bool, bool, bool)"/>.
        /// </remarks>
        /// <value>
        /// The output as a <c>JArray</c> object or <c>null</c>.
        /// </value>
        public JArray History { get; set; }
    }
}
