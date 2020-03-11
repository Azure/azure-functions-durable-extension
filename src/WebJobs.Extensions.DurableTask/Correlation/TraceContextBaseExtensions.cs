using System;
using System.Collections.Generic;
using System.Text;
using DurableTask.Core;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation
{
#if !FUNCTIONS_V1

    /// <summary>
    /// TraceContextBase extension methods.
    /// </summary>
    public static class TraceContextBaseExtensions
    {
        /// <summary>
        /// Create RequestTelemetry from the TraceContext.
        /// </summary>
        /// <param name="context">TraceContext.</param>
        /// <returns>RequestTelemetry.</returns>
        public static RequestTelemetry CreateRequestTelemetry(this TraceContextBase context)
        {
            var telemetry = new RequestTelemetry { Name = context.OperationName };
            telemetry.Duration = context.Duration;
            telemetry.Timestamp = context.StartTime;
            telemetry.Id = context.TelemetryId;
            telemetry.Context.Operation.Id = context.TelemetryContextOperationId;
            telemetry.Context.Operation.ParentId = context.TelemetryContextOperationParentId;

            return telemetry;
        }

        /// <summary>
        /// Create DependencyTelemetry from the Activity.
        /// </summary>
        /// <param name="context">TraceContext.</param>
        /// <returns>DependencyTelemetry.</returns>
        public static DependencyTelemetry CreateDependencyTelemetry(this TraceContextBase context)
        {
            var telemetry = new DependencyTelemetry { Name = context.OperationName };
            telemetry.Start(); // TODO Check if it is necessary.
            telemetry.Duration = context.Duration;
            telemetry.Timestamp = context.StartTime; // TimeStamp is the time of ending the Activity.
            telemetry.Id = context.TelemetryId;
            telemetry.Context.Operation.Id = context.TelemetryContextOperationId;
            telemetry.Context.Operation.ParentId = context.TelemetryContextOperationParentId;

            return telemetry;
        }
    }
#endif
}
