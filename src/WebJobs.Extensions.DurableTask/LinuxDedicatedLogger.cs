// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class LinuxDedicatedLogger : LinuxAppServiceILogger
    {
        private const string LoggingPath = "/var/log/functionsLogs/durableevents.log";

        public override void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            string jsonString = this.GenerateJsonStringToLog(state);
            using (var writer = new StreamWriter(LoggingPath, true))
            {
                // TODO: ensure this won't crash the process if an exception
                // is encountered
                Task unused = writer.WriteLineAsync(jsonString);
            }
        }
    }
}