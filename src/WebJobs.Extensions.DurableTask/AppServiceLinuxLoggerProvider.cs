// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class AppServiceLinuxLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {

            LinuxAppServiceILogger logger;
            // TODO: not sure that this is the correct use of categoryName,
            // but this is what I roughly imagine this method should do.
            if (categoryName == "LinuxDedicatedLogger")
            {
                logger = new LinuxDedicatedLogger();
            }
            else if (categoryName == "LinuxConsumptionLogger")
            {
                logger = new LinuxConsumptionLogger();

            }
            else
            {
                throw new System.Exception("invalid category");
            }

            return logger;
        }

        public void Dispose()
        {
            // Nothing to dispose, so this is a no-op
        }
    }
}
