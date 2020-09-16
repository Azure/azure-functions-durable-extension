// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class AppServiceLinuxLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            // TODO: I do not know if it's ok to throw away the categoryName
            bool inLinuxConsumption = SystemEnvironment.Instance.IsLinuxConsumtpion();
            AppServiceLinuxLogger logger = new AppServiceLinuxLogger(inLinuxConsumption);
            return logger;
        }

        public void Dispose()
        {
            // Nothing to dispose, so this is a no-op
        }
    }
}
