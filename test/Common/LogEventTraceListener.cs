// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public sealed class LogEventTraceListener : IDisposable
    {
        private readonly bool preferFormattedMessages;
        private TraceEventSession currentSession;
        private Thread backgroundTraceThread;

        public LogEventTraceListener()
            : this(preferFormattedMessages: false)
        {
        }

        public LogEventTraceListener(bool preferFormattedMessages)
        {
            this.preferFormattedMessages = preferFormattedMessages;
        }

        public event EventHandler<TraceLogEventArgs> OnTraceLog;

        public void CaptureLogs(
            string sessionName,
            IDictionary<string, TraceEventLevel> providers,
            IDictionary<string, IEnumerable<int>> eventIdFilters = null)
        {
            if (string.IsNullOrEmpty(sessionName))
            {
                throw new ArgumentException(nameof(sessionName));
            }

            if (providers == null)
            {
                throw new ArgumentException(nameof(providers));
            }

            if (this.currentSession != null)
            {
                throw new InvalidOperationException("A trace session is already running.");
            }

            this.backgroundTraceThread = new Thread(_ =>
            {
                Thread.CurrentThread.Name = $"ListenForEventTraceLogs: {sessionName}";

                this.currentSession = new TraceEventSession(sessionName);
                this.currentSession.Source.Dynamic.All += data =>
                {
                    EventHandler<TraceLogEventArgs> handler = this.OnTraceLog;
                    if (handler == null)
                    {
                        return;
                    }

                    // Ignore events that seem to come from MDS infrastructure.
                    if (ShouldExcludeEvent(data, eventIdFilters))
                    {
                        return;
                    }

                    var builder = new StringBuilder(1024);
                    builder.Append(data.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")).Append(':');
                    builder.Append(" [").Append(data.ActivityID.ToString("N").Substring(0, 4));
                    builder.Append(", ").Append(data.RelatedActivityID.ToString("N").Substring(0, 4)).Append("] ");
                    builder.Append(data.EventName).Append(": ");

                    if (this.preferFormattedMessages && !string.IsNullOrEmpty(data.FormattedMessage))
                    {
                        builder.Append(data.FormattedMessage);
                    }
                    else
                    {
                        for (int i = 0; i < data.PayloadNames.Length; i++)
                        {
                            builder.Append(data.PayloadNames[i]).Append('=').Append(data.PayloadValue(i));
                            builder.Append(", ");
                        }

                        // remove trailing ", "
                        builder.Remove(builder.Length - 2, 2);
                    }

                    string message = builder.ToString();
                    var eventArgs = new TraceLogEventArgs(data.ProviderName, data.Level, message);
                    handler(this, eventArgs);
                };

                foreach (KeyValuePair<string, TraceEventLevel> provider in providers)
                {
                    this.currentSession.EnableProvider(provider.Key, provider.Value);
                }

                // This is a blocking call.
                this.currentSession.Source.Process();
            });

            this.backgroundTraceThread.IsBackground = true;
            this.backgroundTraceThread.Start();
        }

        private static bool ShouldExcludeEvent(TraceEvent traceEvent, IDictionary<string, IEnumerable<int>> eventIdFilters)
        {
            // infrastructure events
            if (traceEvent.EventName == "EventSourceMessage" || traceEvent.EventName == "ManifestData")
            {
                return true;
            }

            // explicitly filtered events
            if (eventIdFilters != null &&
                eventIdFilters.TryGetValue(traceEvent.ProviderName, out IEnumerable<int> filteredEvents))
            {
                return filteredEvents.Contains((int)traceEvent.ID);
            }

            return false;
        }

        public void Stop()
        {
            this.currentSession?.Source.StopProcessing();
            this.backgroundTraceThread?.Join(TimeSpan.FromSeconds(10));
            this.currentSession?.Dispose();
            this.currentSession = null;
        }

        public void Dispose()
        {
            this.Stop();
        }

        public class TraceLogEventArgs : EventArgs
        {
            public TraceLogEventArgs(string providerName, TraceEventLevel level, string message)
            {
                this.ProviderName = providerName ?? throw new ArgumentNullException(nameof(providerName));
                this.Level = level;
                this.Message = message ?? throw new ArgumentNullException(nameof(message));
            }

            public string ProviderName { get; }

            public TraceEventLevel Level { get; }

            public string Message { get; }
        }
    }
}
