// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using DurableTask.Core.History;
using DurableTask.Core.Tracing;

#nullable enable
namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation
{
    /// <summary>
    /// Provides helper methods for tracing orchestration activities.
    /// </summary>
    internal class TraceHelper
    {
        private const string Source = "WebJobs.Extensions.DurableTask";

        private static readonly ActivitySource ActivityTraceSource = new ActivitySource(Source);

        internal static Activity? StartActivityForNewOrchestration(ExecutionStartedEvent startEvent, ActivityContext parentTraceContext)
        {
            // Start the new activity to represent scheduling the orchestration
            Activity? newActivity = ActivityTraceSource.StartActivity(
                Schema.SpanNames.CreateOrchestration(startEvent.Name, startEvent.Version),
                kind: ActivityKind.Producer,
                parentContext: parentTraceContext);

            if (newActivity == null)
            {
                return null;
            }

            newActivity.SetTag(Schema.Task.Type, TraceActivityConstants.Orchestration);
            newActivity.SetTag(Schema.Task.Name, startEvent.Name);
            newActivity.SetTag(Schema.Task.InstanceId, startEvent.OrchestrationInstance.InstanceId);
            newActivity.SetTag(Schema.Task.ExecutionId, startEvent.OrchestrationInstance.ExecutionId);

            if (!string.IsNullOrEmpty(startEvent.Version))
            {
                newActivity.SetTag(Schema.Task.Version, startEvent.Version);
            }

            // Set the parent trace context for the ExecutionStartedEvent
            startEvent.ParentTraceContext = new DistributedTraceContext(newActivity.Id!, newActivity.TraceStateString);

            return newActivity;
        }

        internal static Activity? StartActivityForCallingOrSignalingEntity(string targetEntityId, string entityName, string operationName, bool signalEntity, DateTime? scheduledTime, ActivityContext? parentTraceContext, DateTimeOffset startTime = default, string? entityId = null)
        {
            // We only want to create a trace activity for calling or signaling an entity in the case that we can successfully get the parent trace context of the request.
            // Otherwise, we will create an unlinked trace activity with no parent.
            if (parentTraceContext == null)
            {
                return null;
            }

            Activity? newActivity = ActivityTraceSource.StartActivity(
                Schema.SpanNames.CallOrSignalEntity(entityName, operationName),
                kind: signalEntity ? ActivityKind.Producer : ActivityKind.Client,
                parentContext: parentTraceContext.Value,
                startTime: startTime);

            if (newActivity == null)
            {
                return null;
            }

            newActivity.SetTag(Schema.Task.Type, TraceActivityConstants.Entity);
            newActivity.SetTag(Schema.Task.Operation, signalEntity ? TraceActivityConstants.SignalEntity : TraceActivityConstants.CallEntity);
            newActivity.SetTag(Schema.Task.EventTargetInstanceId, targetEntityId);

            if (!string.IsNullOrEmpty(entityId))
            {
                newActivity.SetTag(Schema.Task.InstanceId, entityId);
            }

            if (scheduledTime != null)
            {
                newActivity.SetTag(Schema.Task.ScheduledTime, scheduledTime.Value.ToString());
            }

            return newActivity;
        }

        internal static Activity? StartActivityForProcessingEntityInvocation(string entityId, string entityName, string operationName, bool signalEntity, ActivityContext parentTraceContext, DateTimeOffset startTime)
        {
            Activity? newActivity = ActivityTraceSource.StartActivity(
                Schema.SpanNames.CallOrSignalEntity(entityName, operationName),
                kind: signalEntity ? ActivityKind.Consumer : ActivityKind.Server,
                parentContext: parentTraceContext,
                startTime: startTime);

            if (newActivity == null)
            {
                return null;
            }

            newActivity.SetTag(Schema.Task.Type, TraceActivityConstants.Entity);
            newActivity.SetTag(Schema.Task.Operation, signalEntity ? TraceActivityConstants.SignalEntity : TraceActivityConstants.CallEntity);
            newActivity.SetTag(Schema.Task.InstanceId, entityId);

            return newActivity;
        }

        internal static Activity? StartActivityForEntityStartingAnOrchestration(string entityId, string entityName, string targetInstanceId, ActivityContext? parentTraceContext)
        {
            // We only want to create a trace activity for an entity starting an orchestration in the case that we can successfully get the parent trace context of the request.
            // Otherwise, we will create an unlinked trace activity with no parent.
            if (parentTraceContext == null)
            {
                return null;
            }

            Activity? newActivity = ActivityTraceSource.StartActivity(
                Schema.SpanNames.EntityStartsAnOrchestration(entityName),
                kind: ActivityKind.Producer,
                parentContext: parentTraceContext.Value);

            if (newActivity == null)
            {
                return null;
            }

            newActivity.SetTag(Schema.Task.Type, TraceActivityConstants.Entity);
            newActivity.SetTag(Schema.Task.EventTargetInstanceId, targetInstanceId);
            newActivity.SetTag(Schema.Task.InstanceId, entityId);

            return newActivity;
        }

        internal static void StartActivityUsingTraceContext(ActivityContext traceContext)
        {
            Activity? newActivity = ActivityTraceSource.StartActivity(ActivityKind.Internal);

            if (newActivity != null)
            {
                newActivity.ActivityTraceFlags = traceContext.TraceFlags;
                newActivity.SetTraceId(traceContext.TraceId.ToString());
                newActivity.SetSpanId(traceContext.SpanId.ToString());
                newActivity.SetTraceState(traceContext.TraceState);
            }
        }
    }
}
