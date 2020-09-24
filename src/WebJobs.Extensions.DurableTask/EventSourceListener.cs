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

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (DurableTaskExtension.InLinux)
            {
                // TODO: Not sure that LogLevel here is right
                this.logger.Log(LogLevel.Information, eventData.EventId, eventData, null, null);
            }
        }
    }
}
