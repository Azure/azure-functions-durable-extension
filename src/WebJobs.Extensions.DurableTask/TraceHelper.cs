// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using DurableTask.Core.History;
using DurableTask.Core.Tracing;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation;

#nullable enable
namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
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
            Activity? newActivity = ActivityTraceSource.CreateActivity(
                name: Schema.SpanNames.CreateOrchestration(startEvent.Name, startEvent.Version),
                kind: ActivityKind.Producer,
                parentContext: parentTraceContext);

            newActivity?.Start();

            if (newActivity != null && !string.IsNullOrEmpty(newActivity.Id))
            {
                newActivity.SetTag(Schema.Task.Type, TraceActivityConstants.Orchestration);
                newActivity.SetTag(Schema.Task.Name, startEvent.Name);
                newActivity.SetTag(Schema.Task.InstanceId, startEvent.OrchestrationInstance.InstanceId);
                newActivity.SetTag(Schema.Task.ExecutionId, startEvent.OrchestrationInstance.ExecutionId);

                if (!string.IsNullOrEmpty(startEvent.Version))
                {
                    newActivity.SetTag(Schema.Task.Version, startEvent.Version);
                }

                // Set the parent trace context for the ExecutionStartedEvent
                startEvent.ParentTraceContext = new DistributedTraceContext(newActivity?.Id!, newActivity?.TraceStateString);
            }

            return newActivity;
        }

        internal static Activity? StartActivityForCallingOrSignalingEntity(string targetEntityId, string entityName, string entityOperation, bool signalEntity, ActivityContext? parentTraceContext, string? entityId = null)
        {
            Activity? newActivity = ActivityTraceSource.CreateActivity(
                name: Schema.SpanNames.CallOrSignalEntity(entityName, entityOperation),
                kind: signalEntity ? ActivityKind.Producer : ActivityKind.Client,
                parentContext: parentTraceContext ?? default);

            newActivity?.Start();

            if (newActivity != null && !string.IsNullOrEmpty(newActivity.Id))
            {
                newActivity.SetTag(Schema.Entity.Type, TraceActivityConstants.Entity);
                newActivity.SetTag(Schema.Entity.EntityOperation, signalEntity ? TraceActivityConstants.SignalEntity : TraceActivityConstants.CallEntity);
                newActivity.SetTag(Schema.Entity.TargetEntityId, targetEntityId);
                if (entityId != null)
                {
                    newActivity.SetTag(Schema.Entity.EntityId, entityId);
                }
            }

            return newActivity;
        }

        internal static Activity? StartActivityForProcessingEntityInvocation(string entityId, string entityName, string entityOperation, bool signalEntity, ActivityContext? parentTraceContext)
        {
            Activity? newActivity = ActivityTraceSource.CreateActivity(
                name: Schema.SpanNames.CallOrSignalEntity(entityName, entityOperation),
                kind: signalEntity ? ActivityKind.Consumer : ActivityKind.Server,
                parentContext: parentTraceContext ?? default);

            newActivity?.Start();

            if (newActivity != null && !string.IsNullOrEmpty(newActivity.Id))
            {
                newActivity.SetTag(Schema.Entity.Type, TraceActivityConstants.Entity);
                newActivity.SetTag(Schema.Entity.EntityOperation, signalEntity ? TraceActivityConstants.SignalEntity : TraceActivityConstants.CallEntity);
                newActivity.SetTag(Schema.Entity.EntityId, entityId);
            }

            return newActivity;
        }

        internal static Activity? StartActivityForEntityStartingAnOrchestration(string entityId, string targetInstanceId, ActivityContext? parentTraceContext)
        {
            Activity? newActivity = ActivityTraceSource.CreateActivity(
                name: Schema.SpanNames.EntityStartsAnOrchestration(),
                kind: ActivityKind.Producer,
                parentContext: parentTraceContext ?? default);

            newActivity?.Start();

            if (newActivity != null && !string.IsNullOrEmpty(newActivity.Id))
            {
                newActivity.SetTag(Schema.Entity.Type, TraceActivityConstants.Entity);
                newActivity.SetTag(Schema.Entity.TargetInstanceId, targetInstanceId);
                newActivity.SetTag(Schema.Entity.EntityId, entityId);
            }

            return newActivity;
        }
    }
}
