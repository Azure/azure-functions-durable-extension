// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class LinuxAppServiceLogger
    {
        private const string ConsolePrefix = "MS_DURABLE_FUNCTION_EVENTS_LOGS";
        private const string LoggingPath = "/var/log/functionsLogs/durableevents.log";
        private readonly bool writeToConsole;

        public LinuxAppServiceLogger(bool writeToConsole)
        {
            // If writeToConsole is False, we write to a file
            this.writeToConsole = writeToConsole;
        }

        public string GenerateJson<TState>(TState maybeEventData)
        {
            string jsonString = "";
            if (maybeEventData is EventWrittenEventArgs)
            {
                var eventData = maybeEventData as EventWrittenEventArgs;

                var values = eventData.Payload;
                var keys = eventData.PayloadNames;

                JObject json = new JObject();
                for (int i = 0; i < values.Count; i++)
                {
                    json.Add(keys[i], (JToken)values[i]);
                }

                jsonString = json.ToString(Newtonsoft.Json.Formatting.None);
            }

            return jsonString;
        }

        public void Log<TState>(TState state)
        {
            string jsonString = this.GenerateJson(state);
            if (jsonString == "")
            {
                return;
            }

            if (this.writeToConsole)
            {
                string consoleLine = ConsolePrefix + " " + jsonString;
                Console.WriteLine(consoleLine);
            }
            else
            {
                using (var writer = new StreamWriter(LoggingPath, true))
                {
                    // We're ignoring exceptions in case one where to occur
                    // in the unobserved task.
                    Task unused = writer.WriteLineAsync(jsonString);
                }
            }
        }
    }
}
