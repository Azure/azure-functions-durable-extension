// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Options
{
    /// <summary>
    /// Represents configuration options for Distributed Tracing in Durable Functions.
    /// </summary>
    public enum DurableDistributedTracingVersion
    {
        /// <summary>
        /// Distributed Tracing is disabled.
        /// </summary>
        None = 0,

        /// <summary>
        /// Original implementation of Distributed Tracing in Durable Functions
        /// that can be configured to use HttpCorrelationProtocol or W3CTraceContext.
        /// </summary>
        V1 = 1,

        /// <summary>
        /// OpenTelemetry compatible version of Distributed Tracing in Durable Functions.
        /// </summary>
        V2 = 2,
    }
}
