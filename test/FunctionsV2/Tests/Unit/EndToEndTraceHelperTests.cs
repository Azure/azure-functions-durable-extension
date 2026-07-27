// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace WebJobs.Extensions.DurableTask.Tests.V2
{
    public class EndToEndTraceHelperTests
    {
        private const string HubName = "TestHub";
        private const string FunctionName = "TraceFunction";
        private const string InstanceId = "trace-instance";
        private const string Input = "sensitive input";
        private const string Output = "sensitive output";
        private const string OperationInput = "operation input";
        private const string OperationOutput = "operation output";
        private const string SerializedException = "serialized exception";

        public enum ReplayTraceMethod
        {
            FunctionStarting,
            FunctionCompleted,
            FunctionFailedException,
            FunctionFailedStrings,
            OperationCompleted,
            OperationFailedException,
            OperationFailedString,
            ExternalEventRaised,
            EntityResponseReceived,
        }

        public static IEnumerable<object[]> ReplayTraceCases
        {
            get
            {
                foreach (ReplayTraceMethod traceMethod in Enum.GetValues(typeof(ReplayTraceMethod)))
                {
                    foreach (bool shouldTraceRawData in new[] { false, true })
                    {
                        yield return new object[] { traceMethod, false, false, shouldTraceRawData };
                        yield return new object[] { traceMethod, false, true, shouldTraceRawData };
                        yield return new object[] { traceMethod, true, false, shouldTraceRawData };
                        yield return new object[] { traceMethod, true, true, shouldTraceRawData };
                    }
                }
            }
        }

        public static IEnumerable<object[]> ExceptionReplayCases
        {
            get
            {
                foreach (ReplayTraceMethod traceMethod in new[]
                {
                    ReplayTraceMethod.FunctionFailedException,
                    ReplayTraceMethod.OperationFailedException,
                })
                {
                    yield return new object[] { traceMethod, false, false };
                    yield return new object[] { traceMethod, false, true };
                    yield return new object[] { traceMethod, true, false };
                    yield return new object[] { traceMethod, true, true };
                }
            }
        }

        [Theory]
        [InlineData(true, "DO NOT LOG ME")]
        [InlineData(false, "DO NOT LOG ME")]
        [InlineData(true, null)]
        [InlineData(false, null)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void StringSanitizerTest(
            bool shouldTraceRawData,
            string? possiblySensitiveData)
        {
            // set up trace helper
            var nullLogger = new NullLogger<EndToEndTraceHelper>();
            var traceHelper = new EndToEndTraceHelper(
                logger: nullLogger,
                traceReplayEvents: false, // has not effect on sanitizer
                shouldTraceRawData: shouldTraceRawData);

            // run sanitizer
            traceHelper.SanitizeString(
                rawPayload: possiblySensitiveData,
                out string iLoggerString,
                out string kustoTableString);

            // expected: sanitized string should not contain the sensitive data
            // skip this check if data is null
            if (possiblySensitiveData != null)
            {
                Assert.DoesNotContain(possiblySensitiveData, kustoTableString);
            }

            if (shouldTraceRawData)
            {
                string expectedString = possiblySensitiveData ?? string.Empty;
                Assert.Equal(expectedString, iLoggerString);
            }
            else
            {
                // If raw data is not being traced,
                // kusto and the ilogger should get the same data
                Assert.Equal(iLoggerString, kustoTableString);
            }
        }

        [Theory]
        [InlineData(true, "DO NOT LOG ME")]
        [InlineData(false, "DO NOT LOG ME")]
        [InlineData(true, null)]
        [InlineData(false, null)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ExceptionSanitizerTest(
            bool shouldTraceRawData,
            string? possiblySensitiveData)
        {
            // set up trace helper
            var nullLogger = new NullLogger<EndToEndTraceHelper>();
            var traceHelper = new EndToEndTraceHelper(
                logger: nullLogger,
                traceReplayEvents: false, // has not effect on sanitizer
                shouldTraceRawData: shouldTraceRawData);

            // exception to sanitize
            Exception? exception = null;
            if (possiblySensitiveData != null)
            {
                exception = new Exception(possiblySensitiveData);
            }

            // run sanitizer
            traceHelper.SanitizeException(
                exception: exception,
                out string iLoggerString,
                out string kustoTableString);

            // exception message should not be part of the sanitized strings
            // skip this check if data is null
            if (possiblySensitiveData != null)
            {
                Assert.DoesNotContain(possiblySensitiveData, kustoTableString);
            }

            if (shouldTraceRawData)
            {
                var expectedString = exception?.ToString() ?? string.Empty;
                Assert.Equal(expectedString, iLoggerString);
            }
            else
            {
                // If raw data is not being traced,
                // kusto and the ilogger should get the same data
                Assert.Equal(iLoggerString, kustoTableString);
            }
        }

        [Theory]
        [MemberData(nameof(ReplayTraceCases))]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ReplayAwareTraceMethodsRespectReplayConfiguration(
            ReplayTraceMethod traceMethod,
            bool isReplay,
            bool traceReplayEvents,
            bool shouldTraceRawData)
        {
            var logEntries = new List<LogEntry>();
            var traceHelper = new EndToEndTraceHelper(
                logger: new TestLogger(logEntries),
                traceReplayEvents: traceReplayEvents,
                shouldTraceRawData: shouldTraceRawData);
            var exception = new InvalidOperationException("operation failure");

            InvokeTraceMethod(traceHelper, traceMethod, isReplay, exception);

            bool shouldLog = !isReplay || traceReplayEvents;
            if (shouldLog)
            {
                LogEntry entry = Assert.Single(logEntries);
                AssertTraceEntry(entry, traceMethod, isReplay, shouldTraceRawData, exception);
            }
            else
            {
                Assert.Empty(logEntries);
            }

            traceHelper.ExtensionWarningEvent(HubName, FunctionName, InstanceId, "sequence probe");

            LogEntry sequenceProbe = logEntries.Last();
            Assert.Equal(shouldLog ? 1L : 0L, GetStateValue<long>(sequenceProbe, "sequenceNumber"));
        }

        [Theory]
        [MemberData(nameof(ExceptionReplayCases))]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void FailureTraceExceptionFormattingRespectsReplayConfiguration(
            ReplayTraceMethod traceMethod,
            bool isReplay,
            bool traceReplayEvents)
        {
            var exception = new TrackingException(throwOnToString: false);
            var traceHelper = new EndToEndTraceHelper(
                logger: new TestLogger(new List<LogEntry>()),
                traceReplayEvents: traceReplayEvents);

            InvokeTraceMethod(traceHelper, traceMethod, isReplay, exception);

            Assert.Equal(!isReplay || traceReplayEvents ? 1 : 0, exception.ToStringCalls);
        }

        [Theory]
        [InlineData(ReplayTraceMethod.FunctionFailedException)]
        [InlineData(ReplayTraceMethod.OperationFailedException)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void SuppressedReplayFailureDoesNotEvaluateThrowingException(ReplayTraceMethod traceMethod)
        {
            var suppressedException = new TrackingException(throwOnToString: true);
            var suppressedTraceHelper = new EndToEndTraceHelper(
                logger: new TestLogger(new List<LogEntry>()),
                traceReplayEvents: false);

            InvokeTraceMethod(suppressedTraceHelper, traceMethod, isReplay: true, suppressedException);

            Assert.Equal(0, suppressedException.ToStringCalls);

            var enabledException = new TrackingException(throwOnToString: true);
            var enabledTraceHelper = new EndToEndTraceHelper(
                logger: new TestLogger(new List<LogEntry>()),
                traceReplayEvents: true);

            Assert.Throws<InvalidOperationException>(
                () => InvokeTraceMethod(enabledTraceHelper, traceMethod, isReplay: true, enabledException));
            Assert.Equal(1, enabledException.ToStringCalls);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ClientOperationReceived_LogsWhenInvocationIdProvided()
        {
            // Arrange
            var logMessages = new List<string>();
            var testLogger = new TestLogger(logMessages);
            var traceHelper = new EndToEndTraceHelper(
                logger: testLogger,
                traceReplayEvents: false);

            // Act
            traceHelper.ClientOperationReceived(
                hubName: "TestHub",
                operationType: "StartOrchestration",
                instanceId: "test-instance-123",
                functionInvocationId: "invocation-456");

            // Assert
            Assert.Single(logMessages);
            Assert.Contains("StartOrchestration", logMessages[0]);
            Assert.Contains("test-instance-123", logMessages[0]);
            Assert.Contains("invocation-456", logMessages[0]);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ClientOperationReceived_DoesNotLogWhenInvocationIdNull()
        {
            // Arrange
            var logMessages = new List<string>();
            var testLogger = new TestLogger(logMessages);
            var traceHelper = new EndToEndTraceHelper(
                logger: testLogger,
                traceReplayEvents: false);

            // Act
            traceHelper.ClientOperationReceived(
                hubName: "TestHub",
                operationType: "StartOrchestration",
                instanceId: "test-instance-123",
                functionInvocationId: null);

            // Assert - should not log when invocation ID is null
            Assert.Empty(logMessages);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ClientOperationReceived_DoesNotLogWhenInvocationIdEmpty()
        {
            // Arrange
            var logMessages = new List<string>();
            var testLogger = new TestLogger(logMessages);
            var traceHelper = new EndToEndTraceHelper(
                logger: testLogger,
                traceReplayEvents: false);

            // Act
            traceHelper.ClientOperationReceived(
                hubName: "TestHub",
                operationType: "Terminate",
                instanceId: "test-instance-123",
                functionInvocationId: string.Empty);

            // Assert - should not log when invocation ID is empty
            Assert.Empty(logMessages);
        }

        private static void InvokeTraceMethod(
            EndToEndTraceHelper traceHelper,
            ReplayTraceMethod traceMethod,
            bool isReplay,
            Exception exception)
        {
            switch (traceMethod)
            {
                case ReplayTraceMethod.FunctionStarting:
                    traceHelper.FunctionStarting(
                        HubName,
                        FunctionName,
                        InstanceId,
                        Input,
                        FunctionType.Orchestrator,
                        isReplay,
                        taskEventId: 42);
                    break;
                case ReplayTraceMethod.FunctionCompleted:
                    traceHelper.FunctionCompleted(
                        HubName,
                        FunctionName,
                        InstanceId,
                        Output,
                        continuedAsNew: false,
                        FunctionType.Orchestrator,
                        isReplay,
                        taskEventId: 42);
                    break;
                case ReplayTraceMethod.FunctionFailedException:
                    traceHelper.FunctionFailed(
                        HubName,
                        FunctionName,
                        InstanceId,
                        exception,
                        FunctionType.Orchestrator,
                        isReplay,
                        taskEventId: 42);
                    break;
                case ReplayTraceMethod.FunctionFailedStrings:
                    traceHelper.FunctionFailed(
                        HubName,
                        FunctionName,
                        InstanceId,
                        reason: "preformatted reason",
                        sanitizedReason: "preformatted sanitized reason",
                        FunctionType.Orchestrator,
                        isReplay,
                        taskEventId: 42);
                    break;
                case ReplayTraceMethod.OperationCompleted:
                    traceHelper.OperationCompleted(
                        HubName,
                        FunctionName,
                        InstanceId,
                        operationId: "operation-id",
                        operationName: "operation",
                        OperationInput,
                        OperationOutput,
                        duration: 12.5,
                        isReplay);
                    break;
                case ReplayTraceMethod.OperationFailedException:
                    traceHelper.OperationFailed(
                        HubName,
                        FunctionName,
                        InstanceId,
                        operationId: "operation-id",
                        operationName: "operation",
                        OperationInput,
                        exception,
                        duration: 12.5,
                        isReplay);
                    break;
                case ReplayTraceMethod.OperationFailedString:
                    traceHelper.OperationFailed(
                        HubName,
                        FunctionName,
                        InstanceId,
                        operationId: "operation-id",
                        operationName: "operation",
                        OperationInput,
                        SerializedException,
                        duration: 12.5,
                        isReplay);
                    break;
                case ReplayTraceMethod.ExternalEventRaised:
                    traceHelper.ExternalEventRaised(
                        HubName,
                        FunctionName,
                        InstanceId,
                        eventName: "event-name",
                        input: Input,
                        isReplay);
                    break;
                case ReplayTraceMethod.EntityResponseReceived:
                    traceHelper.EntityResponseReceived(
                        HubName,
                        FunctionName,
                        FunctionType.Orchestrator,
                        InstanceId,
                        operationId: "operation-id",
                        result: Output,
                        isReplay);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(traceMethod), traceMethod, null);
            }
        }

        private static void AssertTraceEntry(
            LogEntry entry,
            ReplayTraceMethod traceMethod,
            bool isReplay,
            bool shouldTraceRawData,
            Exception exception)
        {
            Assert.Equal(GetExpectedLogLevel(traceMethod), entry.Level);
            Assert.Equal(default(EventId), entry.EventId);
            Assert.Equal(GetExpectedTemplate(traceMethod), GetStateValue<string>(entry, "{OriginalFormat}"));
            Assert.Equal(InstanceId, GetStateValue<string>(entry, "instanceId"));
            Assert.Equal(FunctionName, GetStateValue<string>(entry, "functionName"));
            Assert.Equal(HubName, GetStateValue<string>(entry, "hubName"));
            Assert.Equal(0L, GetStateValue<long>(entry, "sequenceNumber"));
            Assert.Null(entry.Exception);
            Assert.NotEmpty(entry.Message);

            switch (traceMethod)
            {
                case ReplayTraceMethod.FunctionStarting:
                    Assert.Equal(GetLoggerPayload(Input, shouldTraceRawData), GetStateValue<string>(entry, "input"));
                    Assert.Equal(isReplay, GetStateValue<bool>(entry, "isReplay"));
                    Assert.Equal(42, GetStateValue<int>(entry, "taskEventId"));
                    break;
                case ReplayTraceMethod.FunctionCompleted:
                    Assert.Equal(GetLoggerPayload(Output, shouldTraceRawData), GetStateValue<string>(entry, "output"));
                    Assert.Equal(isReplay, GetStateValue<bool>(entry, "isReplay"));
                    Assert.Equal(42, GetStateValue<int>(entry, "taskEventId"));
                    break;
                case ReplayTraceMethod.FunctionFailedException:
                    Assert.Equal(GetLoggerException(exception, shouldTraceRawData), GetStateValue<string>(entry, "reason"));
                    Assert.Equal(isReplay, GetStateValue<bool>(entry, "isReplay"));
                    Assert.Equal(42, GetStateValue<int>(entry, "taskEventId"));
                    break;
                case ReplayTraceMethod.FunctionFailedStrings:
                    Assert.Equal("preformatted reason", GetStateValue<string>(entry, "reason"));
                    Assert.Equal(isReplay, GetStateValue<bool>(entry, "isReplay"));
                    Assert.Equal(42, GetStateValue<int>(entry, "taskEventId"));
                    break;
                case ReplayTraceMethod.OperationCompleted:
                    Assert.Equal(GetLoggerPayload(OperationInput, shouldTraceRawData), GetStateValue<string>(entry, "input"));
                    Assert.Equal(GetLoggerPayload(OperationOutput, shouldTraceRawData), GetStateValue<string>(entry, "output"));
                    Assert.Equal(isReplay, GetStateValue<bool>(entry, "isReplay"));
                    break;
                case ReplayTraceMethod.OperationFailedException:
                    Assert.Equal(GetLoggerPayload(OperationInput, shouldTraceRawData), GetStateValue<string>(entry, "input"));
                    Assert.Equal(GetLoggerException(exception, shouldTraceRawData), GetStateValue<string>(entry, "exception"));
                    Assert.Equal(isReplay, GetStateValue<bool>(entry, "isReplay"));
                    break;
                case ReplayTraceMethod.OperationFailedString:
                    Assert.Equal(GetLoggerPayload(OperationInput, shouldTraceRawData), GetStateValue<string>(entry, "input"));
                    Assert.Equal(GetLoggerPayload(SerializedException, shouldTraceRawData), GetStateValue<string>(entry, "exception"));
                    Assert.Equal(isReplay, GetStateValue<bool>(entry, "isReplay"));
                    break;
                case ReplayTraceMethod.ExternalEventRaised:
                case ReplayTraceMethod.EntityResponseReceived:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(traceMethod), traceMethod, null);
            }
        }

        private static LogLevel GetExpectedLogLevel(ReplayTraceMethod traceMethod)
        {
            return traceMethod == ReplayTraceMethod.FunctionFailedException
                || traceMethod == ReplayTraceMethod.FunctionFailedStrings
                || traceMethod == ReplayTraceMethod.OperationFailedException
                || traceMethod == ReplayTraceMethod.OperationFailedString
                ? LogLevel.Error
                : LogLevel.Information;
        }

        private static string GetExpectedTemplate(ReplayTraceMethod traceMethod)
        {
            switch (traceMethod)
            {
                case ReplayTraceMethod.FunctionStarting:
                    return "{instanceId}: Function '{functionName} ({functionType})' started. IsReplay: {isReplay}. Input: {input}. State: {state}. RuntimeStatus: {runtimeStatus}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}. TaskEventId: {taskEventId}";
                case ReplayTraceMethod.FunctionCompleted:
                    return "{instanceId}: Function '{functionName} ({functionType})' completed. ContinuedAsNew: {continuedAsNew}. IsReplay: {isReplay}. Output: {output}. State: {state}. RuntimeStatus: {runtimeStatus}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}. TaskEventId: {taskEventId}";
                case ReplayTraceMethod.FunctionFailedException:
                case ReplayTraceMethod.FunctionFailedStrings:
                    return "{instanceId}: Function '{functionName} ({functionType})' failed with an error. Reason: {reason}. IsReplay: {isReplay}. State: {state}. RuntimeStatus: {runtimeStatus}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}. TaskEventId: {taskEventId}";
                case ReplayTraceMethod.OperationCompleted:
                    return "{instanceId}: Function '{functionName} ({functionType})' completed '{operationName}' operation {operationId} in {duration}ms. IsReplay: {isReplay}. Input: {input}. Output: {output}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.";
                case ReplayTraceMethod.OperationFailedException:
                case ReplayTraceMethod.OperationFailedString:
                    return "{instanceId}: Function '{functionName} ({functionType})' failed '{operationName}' operation {operationId} after {duration}ms with exception {exception}. Input: {input}. IsReplay: {isReplay}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.";
                case ReplayTraceMethod.ExternalEventRaised:
                    return "{instanceId}: Function '{functionName} ({functionType})' received a '{eventName}' event. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.";
                case ReplayTraceMethod.EntityResponseReceived:
                    return "{instanceId}: Function '{functionName} ({functionType})' received an entity response. OperationId: {operationId}. State: {state}. HubName: {hubName}. AppName: {appName}. SlotName: {slotName}. ExtensionVersion: {extensionVersion}. SequenceNumber: {sequenceNumber}.";
                default:
                    throw new ArgumentOutOfRangeException(nameof(traceMethod), traceMethod, null);
            }
        }

        private static string GetLoggerPayload(string payload, bool shouldTraceRawData)
        {
            return shouldTraceRawData ? payload : $"(Redacted {payload.Length} characters)";
        }

        private static string GetLoggerException(Exception exception, bool shouldTraceRawData)
        {
            return shouldTraceRawData
                ? exception.ToString()
                : $"{exception.GetType().FullName}\n{exception.StackTrace}";
        }

        private static T GetStateValue<T>(LogEntry entry, string key)
        {
            KeyValuePair<string, object?> value = Assert.Single(entry.State, item => item.Key == key);
            return Assert.IsType<T>(value.Value);
        }

        /// <summary>
        /// Simple test logger that captures log messages.
        /// </summary>
        private class TestLogger : ILogger
        {
            private readonly List<string>? messages;
            private readonly List<LogEntry>? entries;

            public TestLogger(List<string> messages)
            {
                this.messages = messages;
            }

            public TestLogger(List<LogEntry> entries)
            {
                this.entries = entries;
            }

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                string message = formatter(state, exception);
                this.messages?.Add(message);
                this.entries?.Add(new LogEntry(
                    logLevel,
                    eventId,
                    state is IEnumerable<KeyValuePair<string, object?>> structuredState
                        ? structuredState.ToList()
                        : new List<KeyValuePair<string, object?>>(),
                    exception,
                    message));
            }
        }

        private sealed class LogEntry
        {
            public LogEntry(
                LogLevel level,
                EventId eventId,
                IReadOnlyList<KeyValuePair<string, object?>> state,
                Exception? exception,
                string message)
            {
                this.Level = level;
                this.EventId = eventId;
                this.State = state;
                this.Exception = exception;
                this.Message = message;
            }

            public LogLevel Level { get; }

            public EventId EventId { get; }

            public IReadOnlyList<KeyValuePair<string, object?>> State { get; }

            public Exception? Exception { get; }

            public string Message { get; }
        }

        private sealed class TrackingException : Exception
        {
            private readonly bool throwOnToString;

            public TrackingException(bool throwOnToString)
                : base("tracking exception")
            {
                this.throwOnToString = throwOnToString;
            }

            public int ToStringCalls { get; private set; }

            public override string ToString()
            {
                this.ToStringCalls++;
                if (this.throwOnToString)
                {
                    throw new InvalidOperationException("TrackingException.ToString was evaluated.");
                }

                return "tracking exception string";
            }
        }
    }
}
