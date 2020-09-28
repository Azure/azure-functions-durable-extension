// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class EventSourceListener : EventListener
    {
        private readonly LinuxAppServiceILogger logger;

        public EventSourceListener(LinuxAppServiceILogger logger)
        {
            this.logger = logger;
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "DurableTask-Core" ||
                eventSource.Name == "DurableTask-AzureStorage" ||
                eventSource.Name == "WebJobs-Extensions-DurableTask")
            {
                this.EnableEvents(eventSource, EventLevel.LogAlways, EventKeywords.All);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            this.logger.Log(LogLevel.Information, eventData.EventId, eventData, null, null);
        }
    }
}
