// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Configuration of the trace options
    /// for the Durable Task Extension.
    /// </summary>
    public class TraceOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether to trace the inputs and outputs of function calls.
        /// </summary>
        /// <remarks>
        /// The default behavior when tracing function execution events is to include the number of bytes in the serialized
        /// inputs and outputs for function calls. This provides minimal information about what the inputs and outputs look
        /// like without bloating the logs or inadvertently exposing sensitive information to the logs. Setting
        /// <see cref="TraceInputsAndOutputs"/> to <c>true</c> will instead cause the default function logging to log
        /// the entire contents of function inputs and outputs.
        /// </remarks>
        /// <value>
        /// <c>true</c> to trace the raw values of inputs and outputs; otherwise <c>false</c>.
        /// </value>
        public bool TraceInputsAndOutputs { get; set; }

        /// <summary>
        /// Gets or sets if Azure linux telemetry should include verbose logs.
        /// </summary>
        /// <remarks>
        /// The default behaviour is false, which disables verbose logs. When set
        /// to true, performance may be affected due to the amount of verbose logs.
        /// We recommend setting this to true primarily for debugging purposes.
        /// <value>
        /// <c>true</c> to enable verbose telemetry; <c>false</c> otherwise.
        /// </value>
        /// </remarks>
        public bool AllowVerboseLinuxTelemetry { get; set; } = false;

        /// <summary>
        /// Gets or sets if logs for replay events need to be recorded.
        /// </summary>
        /// <remarks>
        /// The default value is false, which disables the logging of replay events.
        /// </remarks>
        /// <value>
        /// Boolean value specifying if the replay events should be logged.
        /// </value>
        public bool TraceReplayEvents { get; set; }

#if !FUNCTIONS_V1
        /// <summary>
        /// Gets or sets a flag indicating whether to enable distributed tracing.
        /// The default value is false.
        /// </summary>
        public bool DistributedTracingEnabled { get; set; } = false;

        /// <summary>
        /// Gets or sets a protocol for distributed Tracing.
        /// Possible values are "HttpCorrelationProtocol" and "W3CTraceContext".
        /// The default value is "HttpCorrelationProtocol".
        /// </summary>
        public string DistributedTracingProtocol { get; set; } = "HttpCorrelationProtocol";

#endif
        internal void AddToDebugString(StringBuilder builder)
        {
            builder.Append(nameof(this.TraceReplayEvents)).Append(": ").Append(this.TraceReplayEvents).Append(", ");
            builder.Append(nameof(this.TraceInputsAndOutputs)).Append(": ").Append(this.TraceInputsAndOutputs);
        }
    }
}
