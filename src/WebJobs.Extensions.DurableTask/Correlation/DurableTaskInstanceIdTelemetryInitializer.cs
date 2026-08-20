// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;

#nullable enable

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation
{
    internal class DurableTaskInstanceIdTelemetryInitializer : ITelemetryInitializer
    {
        private readonly bool includeInstanceId;

        public DurableTaskInstanceIdTelemetryInitializer(bool includeInstanceId)
        {
            this.includeInstanceId = includeInstanceId;
        }

        public void Initialize(ITelemetry telemetry)
        {
            if (telemetry is OperationTelemetry operationTelemetry &&
                string.IsNullOrEmpty(telemetry.Context.Operation.Name) &&
                !string.IsNullOrEmpty(operationTelemetry.Name))
            {
                telemetry.Context.Operation.Name = operationTelemetry.Name;
            }

            if (!this.includeInstanceId)
            {
                return;
            }

            Activity? activity = Activity.Current;
            if (activity == null)
            {
                return;
            }

            // Check if it is an orchestration, activity, or entity span
            string? type = activity.GetTagItem(Schema.Task.Type) as string;

            // Support orchestration, activity, and entity spans
            if (type != TraceActivityConstants.Orchestration &&
                type != TraceActivityConstants.Activity &&
                type != TraceActivityConstants.Entity)
            {
                return;
            }

            string? operation = activity.GetTagItem(Schema.Task.Operation) as string;

            // Exclude create_orchestration spans via operation tag
            if (operation == TraceActivityConstants.CreateOrchestration)
            {
                return;
            }

            string? instanceId = activity.GetTagItem(Schema.Task.InstanceId) as string;
            if (!string.IsNullOrEmpty(instanceId))
            {
                if (string.IsNullOrEmpty(telemetry.Context.Operation.Name))
                {
                    telemetry.Context.Operation.Name = activity.DisplayName;
                }

                // Append instance ID to operation name if not already present
                if (!telemetry.Context.Operation.Name.Contains(instanceId))
                {
                    telemetry.Context.Operation.Name = $"{telemetry.Context.Operation.Name} ({instanceId})";
                }
            }
        }
    }
}
