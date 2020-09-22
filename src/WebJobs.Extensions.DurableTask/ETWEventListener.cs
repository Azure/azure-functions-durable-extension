using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class ETWEventListener : EventListener
    {
        private ILogger logger;

        public ETWEventListener(ILogger logger)
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
