using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    public class LinuxLogger : ILogger
    {
        private readonly bool inConsumption;
        private readonly string consolePrefix = "MS_DURABLE_FUNCTION_EVENTS_LOGS";
        private readonly string loggingPath = "/var/log/functionsLogs/durableevents.log";

        public LinuxLogger(bool inConsumption)
        {
            this.inConsumption = inConsumption;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            // TODO: I this we can leave this unimplemented
            throw new NotImplementedException();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            // TODO: assuming these logs should never be disabled
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            string jsonString = ""; // assume that, somehow, this becomes a string-representation of a JSON-representation of an ETW event....
            // @Chris, any ideas on how to set the right value here? I suppose we'll need to be listening to ETW events as well and transform them into a JSON-string here.
            if (this.inConsumption)
            {
                // In Linux Consumption, we write to console
                string consoleLine = this.consolePrefix + " " + jsonString;
                Console.WriteLine(consoleLine);
            }
            else
            {
                // In Linux Dedicated, we write to file
                using (var streamWriter = new StreamWriter(this.loggingPath, true))
                {
                    streamWriter.WriteLine(jsonString);
                }

            }

        }
    }
}
