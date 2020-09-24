// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class EventSourceListener : EventListener
    {
        private readonly LinuxAppServiceILogger logger;
        public static readonly EventSourceListener Instance = Initialize();

        private EventSourceListener(LinuxAppServiceILogger logger)
        {
            this.logger = logger;
        }

        private static EventSourceListener Initialize()
        {
            LinuxAppServiceILogger linuxLogger = null;
            EventSourceListener instance = null;
            if (SystemEnvironment.Instance.IsLinuxConsumption())
            {
                linuxLogger = new LinuxConsumptionLogger();
            }
            else if (SystemEnvironment.Instance.IsLinuxDedicated())
            {
                linuxLogger = new LinuxDedicatedLogger();
            }

            if (linuxLogger != null)
            {
                instance = new EventSourceListener(linuxLogger);
            }

            return instance;
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "DurableTask-Core" ||
                eventSource.Name == "DurableTask-AzureStorage" ||
                eventSource.Name == "WebJobs-Extensions-DurableTask")
            {
                Instance.EnableEvents(eventSource, EventLevel.LogAlways, EventKeywords.All);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            Instance.logger.Log(LogLevel.Information, eventData.EventId, eventData, null, null);
        }
    }
}
