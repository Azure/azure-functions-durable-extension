// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

public class TestLoggerProvider : ILoggerProvider, ILogger
{
    private readonly IMessageSink messageSink;
    private ITestOutputHelper? currentTestOutput;
    private ConcurrentBag<string> logs = new ConcurrentBag<string>();

    public TestLoggerProvider(IMessageSink messageSink)
    {
        this.messageSink = messageSink;
    }

    public IEnumerable<string> CoreToolsLogs => this.logs.ToArray();

    // This needs to be created/disposed per-test so we can associate logs
    // with the specific running test.
    public IDisposable UseTestLogger(ITestOutputHelper testOutput)
    {
        // reset these every test
        this.currentTestOutput = testOutput;
        return new DisposableOutput(this);
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return this;
    }

    public void Dispose()
    {
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        string formattedString = formatter(state, exception);
        this.messageSink.OnMessage(new DiagnosticMessage(formattedString));
        this.logs.Add(formattedString);
        try { this.currentTestOutput?.WriteLine(formattedString); } catch { }
    }

    private class DisposableOutput : IDisposable
    {
        private readonly TestLoggerProvider xunitLogger;

        public DisposableOutput(TestLoggerProvider xunitLogger)
        {
            this.xunitLogger = xunitLogger;
        }

        public void Dispose()
        {
            this.xunitLogger.currentTestOutput = null;
        }
    }
}
