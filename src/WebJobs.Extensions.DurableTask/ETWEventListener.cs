using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class ETWEventListener : EventListener
    {

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            ILogger logger = null; // TODO: need to set
            bool inLinux = true; // TODO: need to set
            if (inLinux)
            {
                // TODO: Not sure that LogLevel here is right
                logger.Log(LogLevel.Information, eventData.EventId, eventData, null, null);
            }
        }
    }
}
