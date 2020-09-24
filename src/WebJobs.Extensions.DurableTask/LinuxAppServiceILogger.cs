// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics.Tracing;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal abstract class LinuxAppServiceILogger : ILogger
    {
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

        public string GenerateJsonStringToLog<TState>(TState maybeEventData)
        {
            if (maybeEventData is EventWrittenEventArgs)
            {
                var eventData = maybeEventData as EventWrittenEventArgs;

                var values = eventData.Payload;
                var keys = eventData.PayloadNames;
                var keyValuePairs = keys.Zip(values, Tuple.Create);

                JObject json = new JObject();
                foreach (Tuple<string, object> keyAndValue in keyValuePairs)
                {
                    json.Add(keyAndValue.Item1, keyAndValue.Item2.ToString());
                }

                string jsonString = json.ToString(Newtonsoft.Json.Formatting.None);
                return jsonString;
            }
            else
            {
                // TODO: we should still log some error
                return "";
            }
        }

        public abstract void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter);
    }
}
