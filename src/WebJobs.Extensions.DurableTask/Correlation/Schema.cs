// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation
{
    internal static class Schema
    {
        internal static class Task
        {
            internal const string Type = "durabletask.type";
            internal const string Name = "durabletask.task.name";
            internal const string Version = "durabletask.task.version";
            internal const string InstanceId = "durabletask.task.instance_id";
            internal const string ExecutionId = "durabletask.task.execution_id";
            internal const string Status = "durabletask.task.status";
            internal const string TaskId = "durabletask.task.task_id";
            internal const string EventTargetInstanceId = "durabletask.event.target_instance_id";
            internal const string FireAt = "durabletask.fire_at";
            internal const string Operation = "durabletask.task.operation";
            internal const string ScheduledTime = "durabletask.task.scheduled_time";
            internal const string ErrorMessage = "durabletask.entity.error_message";
        }

        internal static class Status
        {
            internal const string Code = "otel.status_code";
            internal const string Description = "otel.status_description";
        }

        internal static class SpanNames
        {
            internal static string CallOrSignalEntity(string name, string operation)
                => $"{TraceActivityConstants.Entity}:{name}:{operation}";

            internal static string EntityStartsAnOrchestration(string name)
                => $"{name}:{TraceActivityConstants.CreateOrchestration}";

            internal static string CreateOrchestration(string name, string? version)
               => FormatName(TraceActivityConstants.CreateOrchestration, name, version);

            private static string FormatName(string prefix, string name, string? version)
                => string.IsNullOrEmpty(version) ? $"{prefix}:{name}" : $"{prefix}:{name}@{version}";
        }
    }
}
