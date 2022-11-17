using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Filter option setting for IDurableOrchestrationClient.GetStatusAsync.
    /// </summary>
    public class InstanceFilterOption
    {
        /// <summary>
        /// Boolean marker for including execution history in the response.
        /// </summary>
        public bool ShowHistory { get; set; } = false;

        /// <summary>
        /// Boolean marker for including output in the execution history response.
        /// </summary>
        public bool ShowHistoryOutput { get; set; } = false;

        /// <summary>
        /// Boolean marker for including input in the execution history response.
        /// </summary>
        public bool ShowHistoryInput { get; set; } = false;

        /// <summary>
        /// If set, fetch and return the input for the orchestration instance.
        /// </summary>
        public bool ShowInstanceInput { get; set; } = true;
    }
}
