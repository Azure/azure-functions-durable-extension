// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class ReplaySafeLogger : ILogger
    {
        private readonly IDurableOrchestrationContext context;
        private readonly ILogger logger;

        internal ReplaySafeLogger(IDurableOrchestrationContext context, ILogger logger)
        {
            this.context = context;
            this.logger = logger;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return this.logger.BeginScope<TState>(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return this.logger.IsEnabled(logLevel);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!this.context.IsReplaying)
            {
                this.logger.Log<TState>(logLevel, eventId, state, exception, formatter);
            }
        }
    }
}
