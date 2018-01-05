// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.TestCommon
{
    internal class TestLoggerProvider : ILoggerProvider
    {
        private readonly Func<string, LogLevel, bool> filter;

        public TestLoggerProvider(Func<string, LogLevel, bool> filter = null)
        {
            this.filter = filter ?? new LogCategoryFilter().Filter;
        }

        public IList<TestLogger> CreatedLoggers { get; } = new List<TestLogger>();

        public ILogger CreateLogger(string categoryName)
        {
            var logger = new TestLogger(categoryName, this.filter);
            this.CreatedLoggers.Add(logger);
            return logger;
        }

        public IEnumerable<LogMessage> GetAllLogMessages()
        {
            return this.CreatedLoggers.SelectMany(l => l.LogMessages);
        }

        public void Dispose()
        {
        }
    }
}