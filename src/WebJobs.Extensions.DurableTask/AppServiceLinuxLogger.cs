// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    public class AppServiceLinuxLogger : ILogger
    {
        private const string ConsolePrefix = "MS_DURABLE_FUNCTION_EVENTS_LOGS";
        private const string LoggingPath = "/var/log/functionsLogs/durableevents.log";
        private readonly bool inConsumption;

        public AppServiceLinuxLogger(bool inConsumption)
        {
            this.inConsumption = inConsumption;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            // This logger does not accept adding external scopes,
            // so it returns a No-Op Disposable, does nothing when disposed.
            return NoOpDisposable.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            // TODO: assuming these logs should never be disabled
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            // TODO: We are assuming that `state` already has the right data ...
            JObject json = JObject.FromObject(state);

            // TODO: maybe add some fields to the JSON object?
            string jsonString = json.ToString(Formatting.None);
            if (this.inConsumption)
            {
                // In Linux Consumption, we write to console
                string consoleLine = ConsolePrefix + " " + jsonString;
                Console.WriteLine(consoleLine);
            }
            else
            {
                // In Linux Dedicated, we write to file
                using (var writer = new StreamWriter(LoggingPath, true))
                {
                    // TODO: ensure this won't crash the process if an exception
                    // is encountered
                    Task unused = writer.WriteLineAsync(jsonString);
                }
            }
        }
    }
}
