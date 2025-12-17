// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable
using System;
using System.Runtime.CompilerServices;
using DurableTask.Core.History;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[assembly: InternalsVisibleTo("E2ETests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100cd1dabd5a893b40e75dc901fe7293db4a3caf9cd4d3e3ed6178d49cd476969abe74a9e0b7f4a0bb15edca48758155d35a4f05e6e852fff1b319d103b39ba04acbadd278c2753627c95e1f6f6582425374b92f51cca3deb0d2aab9de3ecda7753900a31f70a236f163006beefffe282888f85e3c76d1205ec7dfef7fa472a17b1")]

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Provides custom JSON deserialization for <see cref="HistoryEvent"/> objects, mapping each event type to its corresponding
    /// concrete class. This converter enables polymorphic deserialization of history events based on the EventType
    /// property in the JSON payload.
    /// </summary>
    /// <remarks>
    /// This converter only supports reading (deserialization) and does not support writing (serialization) of <see cref="HistoryEvent"/> objects.
    /// When deserializing, the EventType property in the JSON must be present and correspond to a known <see cref="EventType"/>;
    /// otherwise, a <see cref="JsonSerializationException"/> (for a missing EventType property) or <see cref="NotSupportedException"/>
    /// (for an unknown EventType) will be thrown.
    /// </remarks>
    internal class HistoryEventJsonConverter : JsonConverter
    {
        /// <inheritdoc/>
        public override bool CanWrite => false;

        /// <inheritdoc/>
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(HistoryEvent);
        }

        /// <inheritdoc/>
        /// <exception cref="JsonSerializationException"> If the EventType property is missing in the JSON object attempted to be deserialized.</exception>
        /// <exception cref="InvalidOperationException">If the EventType property does not correspond to a known <see cref="EventType"/>.</exception>
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
