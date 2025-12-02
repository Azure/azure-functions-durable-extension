// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable
using System;
using DurableTask.Core.History;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Provides custom JSON deserialization for <see cref="HistoryEvent"> objects, mapping each event type to its corresponding
    /// concrete class. This converter enables polymorphic deserialization of history events based on the EventType
    /// property in the JSON payload.
    /// </summary>
    /// <remarks>
    /// This converter only supports reading (deserialization) and does not support writing (serialization) of <see cref="HistoryEvent"> objects.
    /// When deserializing, the EventType property in the JSON must be present and correspond to a known <see cref="EventType">;
    /// otherwise, a <see cref="JsonSerializationException"/> (for a missing EventType property) or <see cref="NotSupportedException"/>
    /// (for an unknown EventType) will be thrown.
    /// </remarks>
    public class HistoryEventJsonConverter : JsonConverter
    {
        /// <inheritdoc/>
        public override bool CanWrite => false;

        /// <inheritdoc/>
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(HistoryEvent);
        }

        /// <inheritdoc/>
        /// <throws><see cref="JsonSerializationException"/> If the EventType property is missing in the JSON object attempted to be deserialized.</throws>
        /// <throws><see cref="InvalidOperationException"/>If the EventType property does not correspond to a known <see cref="EventType"/>.</throws>
        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            var jo = JObject.Load(reader);
            int eventType = jo["EventType"]?.Value<int>()
                ?? throw new JsonSerializationException("EventType missing");

            Type concreteType = (EventType)eventType switch
            {
                EventType.ExecutionStarted => typeof(ExecutionStartedEvent),
                EventType.ExecutionCompleted => typeof(ExecutionCompletedEvent),
                EventType.TaskScheduled => typeof(TaskScheduledEvent),
                EventType.TaskCompleted => typeof(TaskCompletedEvent),
                EventType.TaskFailed => typeof(TaskFailedEvent),
                EventType.SubOrchestrationInstanceCreated => typeof(SubOrchestrationInstanceCreatedEvent),
                EventType.SubOrchestrationInstanceCompleted => typeof(SubOrchestrationInstanceCompletedEvent),
                EventType.SubOrchestrationInstanceFailed => typeof(SubOrchestrationInstanceFailedEvent),
                EventType.TimerCreated => typeof(TimerCreatedEvent),
                EventType.TimerFired => typeof(TimerFiredEvent),
                EventType.OrchestratorStarted => typeof(OrchestratorStartedEvent),
                EventType.OrchestratorCompleted => typeof(OrchestratorCompletedEvent),
                EventType.EventSent => typeof(EventSentEvent),
                EventType.EventRaised => typeof(EventRaisedEvent),
                EventType.GenericEvent => typeof(GenericEvent),
                EventType.ContinueAsNew => typeof(ContinueAsNewEvent),
                EventType.ExecutionTerminated => typeof(ExecutionTerminatedEvent),
                EventType.ExecutionSuspended => typeof(ExecutionSuspendedEvent),
                EventType.ExecutionResumed => typeof(ExecutionResumedEvent),
                EventType.ExecutionRewound => typeof(ExecutionRewoundEvent),
                EventType.HistoryState => typeof(HistoryStateEvent),
                _ => throw new NotSupportedException($"Unknown HistoryEvent type {eventType}")
            };

            return jo.ToObject(concreteType, serializer);
        }

        /// <inheritdoc/>
        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
