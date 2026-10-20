// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics.Tracing;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal sealed class SdkUsageEventListener : EventListener
    {
        private readonly string hubName;

        public SdkUsageEventListener(string hubName)
        {
            this.hubName = hubName;
        }

        public ConcurrentQueue<EventWrittenEventArgs> Events { get; } = new ConcurrentQueue<EventWrittenEventArgs>();

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "WebJobs-Extensions-DurableTask")
            {
                this.EnableEvents(eventSource, EventLevel.LogAlways);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventId == 236 &&
                eventData.Payload?.Count == 6 &&
                Equals(eventData.Payload[0], this.hubName))
            {
                this.Events.Enqueue(eventData);
            }
        }
    }
}
