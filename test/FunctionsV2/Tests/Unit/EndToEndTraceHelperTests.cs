// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
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

        [Theory]
        [InlineData((int)FunctionType.Entity, "@counter@42")]
        [InlineData((int)FunctionType.Orchestrator, "child-orchestration-id")]
        [InlineData((int)FunctionType.Activity, null)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void FunctionScheduled_LogsTargetInstanceIdInStructuredState(
            int functionType,
            string? targetInstanceId)
        {
            // Arrange
            var testLogger = new TestLogger(null!, category: "UnitTest");
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
            Assert.Equal(targetInstanceId, targetInstanceIdState.Value);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ClientOperationReceived_LogsWhenInvocationIdProvided()
        {
            // Arrange
            var testLogger = new TestLogger(null!, category: "UnitTest");
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
            var testLogger = new TestLogger(null!, category: "UnitTest");
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
            var testLogger = new TestLogger(null!, category: "UnitTest");
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
    }
}
