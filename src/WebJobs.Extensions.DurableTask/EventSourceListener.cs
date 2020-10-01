// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class EventSourceListener : EventListener
    {
        private readonly LinuxAppServiceLogger logger;

        public EventSourceListener(LinuxAppServiceLogger logger)
        {
            this.logger = logger;
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if ((eventSource.Name == "DurableTask-Core"
                  && eventSource.Guid != new Guid("7DA4779A-152E-44A2-A6F2-F80D991A5BEE")) ||
                eventSource.Name == "DurableTask-AzureStorage" ||
                eventSource.Name == "WebJobs-Extensions-DurableTask" ||
                eventSource.Name == "DurableTask-SqlServer")
            {
                this.EnableEvents(eventSource, EventLevel.LogAlways, EventKeywords.All);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            this.logger.Log(eventData);
        }
    }
}
