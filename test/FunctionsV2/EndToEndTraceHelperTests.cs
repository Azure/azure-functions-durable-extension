// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace WebJobs.Extensions.DurableTask.Tests.V2
{
    public class EndToEndTraceHelperTests
    {
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

        /// <summary>
        /// Simple test logger that captures log messages.
        /// </summary>
        private class TestLogger : ILogger
        {
            private readonly List<string> messages;

            public TestLogger(List<string> messages)
            {
                this.messages = messages;
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
                this.messages.Add(formatter(state, exception));
            }
        }
    }
}
