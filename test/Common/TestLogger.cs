// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal class TestLogger : ILogger
    {
        private readonly ITestOutputHelper testOutput;
        private readonly Func<string, LogLevel, bool> filter;
        private readonly ConcurrentQueue<LogMessage> logMessages = new ConcurrentQueue<LogMessage>();

        public TestLogger(ITestOutputHelper testOutput, string category, Func<string, LogLevel, bool> filter = null)
        {
            this.testOutput = testOutput;
            this.Category = category;
            this.filter = filter;
        }

        public string Category { get; }

        public IReadOnlyCollection<LogMessage> LogMessages => this.logMessages;

        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return this.filter?.Invoke(this.Category, logLevel) ?? true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!this.IsEnabled(logLevel))
            {
                return;
            }

            string formattedMessage = formatter(state, exception);
            this.logMessages.Enqueue(new LogMessage
            {
                Level = logLevel,
                EventId = eventId,
                State = state as IEnumerable<KeyValuePair<string, object>>,
                Exception = exception,
                FormattedMessage = formattedMessage,
                Category = this.Category,
            });

            // Only write traces specific to this extension
            if (this.Category == TestHelpers.LogCategory)
            {
                this.testOutput.WriteLine($"    {DateTime.Now:o}: {formattedMessage}");
            }
        }
    }
}