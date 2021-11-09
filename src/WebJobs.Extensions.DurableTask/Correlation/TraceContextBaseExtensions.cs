// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DurableTask.Core;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation
{
    /// <summary>
    /// TraceContextBase extension methods.
    /// </summary>
    public static class TraceContextBaseExtensions
    {
        /// <summary>
        /// Create RequestTelemetry from the TraceContext.
        /// </summary>
        /// <param name="context">TraceContext.</param>
        /// <param name="siteName">Site name.</param>
        /// <returns>RequestTelemetry.</returns>
        public static RequestTelemetry CreateRequestTelemetry(this TraceContextBase context, string siteName)
        {
            var telemetry = new RequestTelemetry { Name = context.OperationName };
            telemetry.Duration = context.Duration;
            telemetry.Timestamp = context.StartTime;
            telemetry.Id = context.TelemetryId;
            telemetry.Context.Operation.Id = context.TelemetryContextOperationId;

            // Operation Name is set as "DtActivity FunctionName" and we only want the "FunctionName" as the operationName
            string[] operationNameArray = context.OperationName.Split(' ');
            bool isActivityRequest = operationNameArray.Length == 2 && string.Equals(operationNameArray[0], TraceConstants.Activity);
            telemetry.Context.Operation.Name = isActivityRequest ? operationNameArray[1] : context.OperationName;

            telemetry.Context.Operation.ParentId = context.TelemetryContextOperationParentId;

            telemetry.Context.Cloud.RoleName = siteName;

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
            telemetry.Start();
            telemetry.Duration = context.Duration;
            telemetry.Timestamp = context.StartTime; // TimeStamp is the time of ending the Activity.
            telemetry.Id = context.TelemetryId;
            telemetry.Context.Operation.Id = context.TelemetryContextOperationId;
            telemetry.Context.Operation.ParentId = context.TelemetryContextOperationParentId;

            return telemetry;
        }
    }
}
