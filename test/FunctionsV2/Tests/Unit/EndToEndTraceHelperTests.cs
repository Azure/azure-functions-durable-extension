// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace WebJobs.Extensions.DurableTask.Tests.V2
{
    public class EndToEndTraceHelperTests
    {
        private readonly ITestOutputHelper output;

        public EndToEndTraceHelperTests(ITestOutputHelper output)
        {
            this.output = output;
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

        // FunctionType is internal, so it cannot appear as a parameter on a public xUnit test
        // method (CS0051). The values are passed as int and cast back inside the test body.
        [Theory]
        [InlineData((int)FunctionType.Entity, "@counter@42", "@counter@42")]
        [InlineData((int)FunctionType.Orchestrator, "child-orchestration-id", "child-orchestration-id")]
        [InlineData((int)FunctionType.Activity, null, null)]

        // Callers that do not supply an instance ID forward an empty string, most notably
        // CallSubOrchestratorAsync(functionName, input). That must be logged as "not supplied"
        // rather than as an empty target instance ID.
        [InlineData((int)FunctionType.Orchestrator, "", null)]
        [InlineData((int)FunctionType.Orchestrator, "   ", null)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void FunctionScheduled_LogsTargetInstanceIdInStructuredState(
            int functionType,
            string? targetInstanceId,
            string? expectedLoggedTargetInstanceId)
        {
            // Arrange
            var testLogger = new TestLogger(this.output, category: "UnitTest");
            var traceHelper = new EndToEndTraceHelper(
                logger: testLogger,
                traceReplayEvents: false);

            // Act
            traceHelper.FunctionScheduled(
                hubName: "TestHub",
                functionName: "TargetFunction",
                instanceId: "parent-instance-id",
                reason: "TestCaller",
                functionType: (FunctionType)functionType,
                isReplay: false,
                targetInstanceId: targetInstanceId);

            // Assert
            var logMessage = Assert.Single(testLogger.LogMessages);
            var state = Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string, object>>>(logMessage.State);
            var targetInstanceIdState = Assert.Single(state, property => property.Key == "targetInstanceId");
            Assert.Equal(expectedLoggedTargetInstanceId, targetInstanceIdState.Value);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void FunctionScheduled_PreservesExistingMessagePrefixAndAppendsTargetInstanceId()
        {
            var testLogger = new TestLogger(this.output, category: "UnitTest");
            var traceHelper = new EndToEndTraceHelper(testLogger, traceReplayEvents: false);

            traceHelper.FunctionScheduled(
                hubName: "TestHub",
                functionName: "Child",
                instanceId: "parent-id",
                reason: "Parent",
                functionType: FunctionType.Orchestrator,
                isReplay: false,
                targetInstanceId: "child-id");

            string message = Assert.Single(testLogger.LogMessages).FormattedMessage;
            Assert.Contains("IsReplay: False. State: Scheduled. RuntimeStatus: Pending.", message);
            Assert.EndsWith("TargetInstanceId: child-id.", message);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void FunctionScheduled_EtwEvent201V3AppendsTargetInstanceId()
        {
            using var listener = new CapturingEventListener();

            EtwEventSource.Instance.FunctionScheduled(
                "hub",
                "app",
                "slot",
                "function",
                "source-id",
                "reason",
                "Entity",
                "version",
                false,
                "@counter@key");

            EventWrittenEventArgs captured = Assert.Single(
                listener.Events,
                item => item.EventId == 201 && item.Payload?.LastOrDefault()?.ToString() == "@counter@key");
            Assert.Equal(3, captured.Version);
            Assert.Equal(
                new[] { "TaskHub", "AppName", "SlotName", "FunctionName", "InstanceId", "Reason", "FunctionType", "ExtensionVersion", "IsReplay", "TargetInstanceId" },
                captured.PayloadNames);
            Assert.Equal(
                new object[] { "hub", "app", "slot", "function", "source-id", "reason", "Entity", "version", false, "@counter@key" },
                captured.Payload);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ClientOperationReceived_LogsWhenInvocationIdProvided()
        {
            // Arrange
            var testLogger = new TestLogger(this.output, category: "UnitTest");
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
            var logMessage = Assert.Single(testLogger.LogMessages);
            Assert.Contains("StartOrchestration", logMessage.FormattedMessage);
            Assert.Contains("test-instance-123", logMessage.FormattedMessage);
            Assert.Contains("invocation-456", logMessage.FormattedMessage);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ClientOperationReceived_DoesNotLogWhenInvocationIdNull()
        {
            // Arrange
            var testLogger = new TestLogger(this.output, category: "UnitTest");
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
            Assert.Empty(testLogger.LogMessages);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ClientOperationReceived_DoesNotLogWhenInvocationIdEmpty()
        {
            // Arrange
            var testLogger = new TestLogger(this.output, category: "UnitTest");
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
            Assert.Empty(testLogger.LogMessages);
        }

        private sealed class CapturingEventListener : EventListener
        {
            public ConcurrentQueue<EventWrittenEventArgs> Events { get; } = new ConcurrentQueue<EventWrittenEventArgs>();

            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                if (eventSource.Name == "WebJobs-Extensions-DurableTask")
                {
                    this.EnableEvents(eventSource, EventLevel.LogAlways);
                }
            }

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                this.Events.Enqueue(eventData);
            }
        }
    }
}
