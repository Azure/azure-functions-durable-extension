// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class LinuxConsumptionLogger : LinuxAppServiceILogger
    {
        private const string ConsolePrefix = "MS_DURABLE_FUNCTION_EVENTS_LOGS";

        public override void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            string jsonString = this.GenerateJsonStringToLog(state);
            string consoleLine = ConsolePrefix + " " + jsonString;
            Console.WriteLine(consoleLine);
        }
    }
}
