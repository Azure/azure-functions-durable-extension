// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Xunit;
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

    /// <summary>
    /// Polls <see cref="CoreToolsLogs"/> until a log line matching <paramref name="predicate"/>
    /// appears, or the <paramref name="maxWaitSeconds"/> timeout elapses. Throws an xUnit
    /// assertion failure if the log is not found. Prefer this over a fixed <c>Task.Delay</c>
    /// to avoid flaky timing issues.
    /// </summary>
    public async Task AssertLogExistsAsync(Func<string, bool> predicate, string? failureMessage = null, int maxWaitSeconds = 10)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(maxWaitSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (this.logs.Any(predicate))
            {
                return;
            }

            await Task.Delay(250);
        }

        // Final check after deadline.
        Assert.True(this.logs.Any(predicate), failureMessage ?? $"Expected log was not found within {maxWaitSeconds}s timeout.");
    }

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
        if (this.currentTestOutput is null)
        {
            Console.WriteLine(formattedString);
        }
        else
        {
            try { this.currentTestOutput.WriteLine(formattedString); } catch { Console.WriteLine(formattedString); }
        }
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
