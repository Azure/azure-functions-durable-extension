// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Diagnostics.Tracing;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    /// <summary>
    /// Lightweight per-test-class listener that delegates to <see cref="SharedTraceSession"/>
    /// instead of creating its own ETW session. This avoids exhausting Windows kernel ETW
    /// session resources when many test classes run in parallel.
    /// </summary>
    public sealed class LogEventTraceListener : IDisposable
    {
        private readonly bool preferFormattedMessages;
        private int subscriberId = -1;

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
            IDictionary<string, TraceEventLevel> providers,
            IDictionary<string, IEnumerable<int>> eventIdFilters = null)
        {
            if (providers == null)
            {
                throw new ArgumentNullException(nameof(providers));
            }

            if (this.subscriberId >= 0)
            {
                throw new InvalidOperationException("CaptureLogs has already been called. Call Stop() before calling CaptureLogs again.");
            }

            this.subscriberId = SharedTraceSession.Subscribe(
                data => this.HandleTraceEvent(data),
                providers,
                eventIdFilters);
        }

        private void HandleTraceEvent(TraceEvent data)
        {
            EventHandler<TraceLogEventArgs> handler = this.OnTraceLog;
            if (handler == null)
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
                if (builder.Length >= 2)
                {
                    builder.Remove(builder.Length - 2, 2);
                }
            }

            string message = builder.ToString();
            var eventArgs = new TraceLogEventArgs(data.ProviderName, data.Level, message);
            handler(this, eventArgs);
        }

        public void Stop()
        {
            if (this.subscriberId >= 0)
            {
                SharedTraceSession.Unsubscribe(this.subscriberId);
                this.subscriberId = -1;
            }
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
