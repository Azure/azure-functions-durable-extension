// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

public class TestLoggerProvider : ILoggerProvider, ILogger
{
    private readonly IMessageSink messageSink;
    private ITestOutputHelper? currentTestOutput;
    private readonly List<string> logsList = new List<string>();
    private readonly object logsLock = new object();

    public TestLoggerProvider(IMessageSink messageSink)
    {
        this.messageSink = messageSink;
    }

    public IEnumerable<string> CoreToolsLogs
    {
        get
        {
            lock (this.logsLock)
            {
                return this.logsList.ToArray();
            }
        }
    }

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
            lock (this.logsLock)
            {
                if (this.logsList.Any(predicate))
                {
                    return;
                }
            }

            await Task.Delay(250);
        }

        // Final check after deadline.
        lock (this.logsLock)
        {
            Assert.True(this.logsList.Any(predicate), failureMessage ?? $"Expected log was not found within {maxWaitSeconds}s timeout.");
        }
    }

    // This needs to be created/disposed per-test so we can associate logs
    // with the specific running test. Tests within the same xUnit collection
    // share a single TestLoggerProvider but run sequentially, so clearing
    // the log list here is safe and ensures each test only sees its own logs.
    public IDisposable UseTestLogger(ITestOutputHelper testOutput)
    {
        this.currentTestOutput = testOutput;
        lock (this.logsLock)
        {
            this.logsList.Clear();
        }

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
        lock (this.logsLock)
        {
            this.logsList.Add(formattedString);
        }

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
