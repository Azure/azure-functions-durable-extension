using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class LinuxLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            // TODO: I do not know if it's ok to throw away the categoryName
            bool inLinuxConsumption = SystemEnvironment.Instance.IsLinuxConsumtpion();
            LinuxLogger logger = new LinuxLogger(inLinuxConsumption);
            return logger;
        }

        public void Dispose()
        {
            // TODO: Not exactly sure how to implement this
            throw new NotImplementedException();
        }
    }
}
