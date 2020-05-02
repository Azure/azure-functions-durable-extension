// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation
{
    /// <summary>
    /// Represents a traceParent that is defined W3C TraceContext.
    /// </summary>
    public class TraceParent
    {
        /// <summary>
        /// Gets or sets the Version of the traceParent.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the TraceId of the traceParent.
        /// </summary>
        public string TraceId { get; set; }

        /// <summary>
        /// Gets or sets the SpanId of the traceParent.
        /// </summary>
        public string SpanId { get; set; }

        /// <summary>
        /// Gets or sets the TraceFlags of the traceParent.
        /// </summary>
        public string TraceFlags { get; set; }
    }
}
