﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.TestCommon
{
    public class TestLoggerProvider : ILoggerProvider
    {
        private readonly Func<string, LogLevel, bool> filter;

        public IList<TestLogger> CreatedLoggers = new List<TestLogger>();

        public TestLoggerProvider(Func<string, LogLevel, bool> filter = null)
        {
            this.filter = filter ?? new LogCategoryFilter().Filter;
        }

        public ILogger CreateLogger(string categoryName)
        {
            var logger = new TestLogger(categoryName, filter);
            CreatedLoggers.Add(logger);
            return logger;
        }

        public IEnumerable<LogMessage> GetAllLogMessages()
        {
            return CreatedLoggers.SelectMany(l => l.LogMessages);
        }

        public void Dispose()
        {
        }
    }
}