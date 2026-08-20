// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Tests
{
    public class TestLoggerProvider : ILoggerProvider
    {
        private readonly ITestOutputHelper output;
        private readonly ConcurrentBag<TestLogger> loggers = new ConcurrentBag<TestLogger>();

        public TestLoggerProvider(ITestOutputHelper output)
        {
            this.output = output;
        }

        public ILogger CreateLogger(string categoryName)
        {
            var logger = new TestLogger(categoryName, this.output);
            this.loggers.Add(logger);
            return logger;
        }

        public IReadOnlyList<string> GetAllLogMessages()
        {
            var messages = new List<string>();
            foreach (var logger in this.loggers)
            {
                messages.AddRange(logger.LogMessages);
            }

            return messages;
        }

        public void Dispose()
        {
        }

        private class TestLogger : ILogger
        {
            private readonly string categoryName;
            private readonly ITestOutputHelper output;
            private readonly List<string> logMessages = new List<string>();

            public TestLogger(string categoryName, ITestOutputHelper output)
            {
                this.categoryName = categoryName;
                this.output = output;
            }

            public IReadOnlyList<string> LogMessages => this.logMessages;

            public IDisposable BeginScope<TState>(TState state) => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                string message = formatter(state, exception);
                this.logMessages.Add(message);

                try
                {
                    this.output.WriteLine($"[{logLevel}] {this.categoryName}: {message}");
                    if (exception != null)
                    {
                        this.output.WriteLine(exception.ToString());
                    }
                }
                catch
                {
                    // xunit output may not be available in some contexts
                }
            }
        }
    }
}
