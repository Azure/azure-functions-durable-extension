// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
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
            var loggerFactory = Mock.Of<ILoggerFactory>(factory => factory.CreateLogger(It.IsAny<string>()) == testLogger);
            var traceHelper = new EndToEndTraceHelper(
                loggerFactory: loggerFactory,
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
            var loggerFactory = Mock.Of<ILoggerFactory>(factory => factory.CreateLogger(It.IsAny<string>()) == testLogger);
            var traceHelper = new EndToEndTraceHelper(loggerFactory, traceReplayEvents: false);

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

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void PerFunctionEvents_UseRecognizedUserCategories()
        {
            using ILoggerFactory factory = this.CreateLoggerFactory(out TestLoggerProvider provider);
            var knownFunctions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "FirstFunction",
                "SecondFunction",
            };
            var helper = new EndToEndTraceHelper(
                factory,
                traceReplayEvents: true,
                shouldTraceRawData: true,
                resolveFunctionName: name => knownFunctions.TryGetValue(name, out string? registeredName) ? registeredName : null);
            var error = new InvalidOperationException("Synthetic error");
            Action<string>[] events =
            {
                name => helper.ExtensionInformationalEvent("hub", "instance", name, "Synthetic message", true),
                name => helper.ExtensionWarningEvent("hub", name, "instance", "Synthetic warning"),
                name => helper.FunctionScheduled("hub", name, "instance", "Caller", FunctionType.Activity, false),
                name => helper.FunctionStarting("hub", name, "instance", "input", FunctionType.Activity, false),
                name => helper.FunctionAwaited("hub", name, FunctionType.Orchestrator, "instance", false),
                name => helper.FunctionListening("hub", name, "instance", "event", false),
                name => helper.FunctionCompleted("hub", name, "instance", "output", false, FunctionType.Orchestrator, false),
                name => helper.FunctionTerminated("hub", name, "instance", "reason"),
                name => helper.SuspendingOrchestration("hub", name, "instance", "reason"),
                name => helper.ResumingOrchestration("hub", name, "instance", "reason"),
                name => helper.FunctionRewound("hub", name, "instance", "reason"),
                name => helper.FunctionFailed("hub", name, "instance", error, FunctionType.Activity, false),
                name => helper.FunctionFailed("hub", name, "instance", "reason", "sanitized reason", FunctionType.Entity, false),
                name => helper.FunctionAborted("hub", name, "instance", "reason", FunctionType.Activity),
                name => helper.OperationCompleted("hub", name, "instance", "operation", "get", "input", "output", 1, false),
                name => helper.OperationFailed("hub", name, "instance", "operation", "get", "input", error, 1, false),
                name => helper.OperationFailed("hub", name, "instance", "operation", "get", "input", "error", 1, false),
                name => helper.ExternalEventRaised("hub", name, "instance", "event", "input", false),
                name => helper.ExternalEventSaved("hub", name, FunctionType.Orchestrator, "instance", "event", false),
                name => helper.EntityOperationQueued("hub", name, "instance", "operation", "get", false),
                name => helper.EntityResponseReceived("hub", name, FunctionType.Orchestrator, "instance", "operation", "result", false),
                name => helper.EntityStateCreated("hub", name, "instance", "get", "operation", false),
                name => helper.EntityStateDeleted("hub", name, "instance", "delete", "operation", false),
                name => helper.EntityLockAcquired("hub", name, "instance", "requester", "execution", "request", false),
                name => helper.EntityLockReleased("hub", name, "instance", "requester", "request", false),
                name => helper.EntityBatchCompleted("hub", name, "instance", 1, 1, 1, 0, 0, 10, 1, 1, "requester", false, "123"),
                name => helper.EntityBatchFailed("hub", name, "instance", "123", error),
                name => helper.EventGridSuccess("hub", name, FunctionState.Started, "instance", "details", HttpStatusCode.OK, "reason", 1),
                name => helper.EventGridFailed("hub", name, FunctionState.Failed, "instance", "details", HttpStatusCode.BadRequest, "reason", 1),
                name => helper.EventGridException("hub", name, FunctionState.Failed, "instance", "details", error, "reason", 1),
                name => helper.TimerExpired("hub", name, "instance", DateTime.UnixEpoch, false),
            };

            foreach (string functionName in new[] { "FirstFunction", "SecondFunction" })
            {
                foreach (Action<string> write in events)
                {
                    write(functionName);
                }

                TestLogger logger = Assert.Single(
                    provider.CreatedLoggers,
                    item => item.Category == LogCategories.CreateFunctionUserCategory(functionName));
                Assert.Equal(events.Length, logger.LogMessages.Count);
                Assert.All(logger.LogMessages, message =>
                {
                    Assert.True(LogCategories.IsFunctionUserCategory(message.Category));
                    Assert.Equal(functionName, message.State.Single(property => property.Key == "functionName").Value);
                });
            }

            Assert.Empty(provider.CreatedLoggers.Single(logger => logger.Category == TestHelpers.LogCategory).LogMessages);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void InfrastructureEvents_KeepTheHostCategory()
        {
            using ILoggerFactory factory = this.CreateLoggerFactory(out TestLoggerProvider provider);
            var helper = new EndToEndTraceHelper(factory.CreateLogger(TestHelpers.LogCategory), traceReplayEvents: false);
            var error = new InvalidOperationException("Synthetic infrastructure error");
            helper.ExtensionInformationalEvent("hub", "", "", "Initialization", writeToUserLogs: true);
            helper.ExtensionInformationalEvent("hub", "", "", "EventSource only", writeToUserLogs: false);
            helper.ExtensionWarningEvent("hub", "", "", "Infrastructure warning");
            helper.ExtensionWarningAnnouncement("Announcement");
            helper.ClientOperationReceived("hub", "StartOrchestration", "instance", "invocation");
            helper.TraceConfiguration("hub", "{}");
            helper.RetrievingToken("hub", "resource");
            helper.TokenRetrievalFailed("hub", "resource", error);
            helper.TokenRenewalFailed("hub", "resource", 1, TimeSpan.Zero, error);

            TestLogger logger = Assert.Single(provider.CreatedLoggers);
            Assert.Equal(TestHelpers.LogCategory, logger.Category);
            Assert.Equal(8, logger.LogMessages.Count);
            Assert.DoesNotContain(logger.LogMessages, message => message.FormattedMessage.Contains("EventSource only", StringComparison.Ordinal));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void PayloadLogging_PreservesUserOptInAndEventSourceSanitization(bool traceRaw)
        {
            using var listener = new CapturingEventListener();
            foreach (FunctionType type in new[] { FunctionType.Activity, FunctionType.Orchestrator, FunctionType.Entity })
            {
                using ILoggerFactory factory = this.CreateLoggerFactory(out TestLoggerProvider provider);
                var helper = new EndToEndTraceHelper(
                    factory,
                    traceReplayEvents: false,
                    shouldTraceRawData: traceRaw,
                    resolveFunctionName: name => name);
                string functionName = type + "Function";
                foreach (string? payload in new[] { null, "", "\"SYNTHETIC_CONTENT\"", "{\"value\":\"SYNTHETIC_CONTENT\"}", "[\"SYNTHETIC_CONTENT\"]", "42", "null" })
                {
                    string instanceId = Guid.NewGuid().ToString();
                    helper.FunctionStarting("hub", functionName, instanceId, payload, type, false);
                    helper.FunctionCompleted("hub", functionName, instanceId, payload, false, type, false);
                    string sanitized = $"(Redacted {payload?.Length ?? 0} characters)";
                    string expected = traceRaw ? payload ?? string.Empty : sanitized;
                    TestLogger logger = provider.CreatedLoggers.Single(item => item.Category == LogCategories.CreateFunctionUserCategory(functionName));
                    LogMessage[] messages = logger.LogMessages.TakeLast(2).ToArray();
                    Assert.Equal(expected, messages[0].State.Single(property => property.Key == "input").Value);
                    Assert.Equal(expected, messages[1].State.Single(property => property.Key == "output").Value);
                    Assert.Contains($". Input: {expected}. State: Started.", messages[0].FormattedMessage);
                    Assert.Contains($". Output: {expected}. State: Completed.", messages[1].FormattedMessage);

                    EventWrittenEventArgs started = Assert.Single(
                        listener.Events,
                        item => item.EventId == 202 && item.Payload?.Contains(instanceId) == true);
                    EventWrittenEventArgs completed = Assert.Single(
                        listener.Events,
                        item => item.EventId == 206 && item.Payload?.Contains(instanceId) == true);
                    Assert.Equal(sanitized, started.Payload![started.PayloadNames!.IndexOf("Input")]);
                    Assert.Equal(sanitized, completed.Payload![completed.PayloadNames!.IndexOf("Output")]);
                }

                Assert.Empty(provider.CreatedLoggers.Single(logger => logger.Category == TestHelpers.LogCategory).LogMessages);
            }
        }

        [Theory]
        [InlineData(false, false, 2)]
        [InlineData(false, true, 0)]
        [InlineData(true, false, 2)]
        [InlineData(true, true, 2)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void UserCategories_PreserveReplayFiltering(bool traceReplay, bool replay, int expectedCount)
        {
            using ILoggerFactory factory = this.CreateLoggerFactory(out TestLoggerProvider provider);
            var helper = new EndToEndTraceHelper(
                factory,
                traceReplay,
                shouldTraceRawData: true,
                resolveFunctionName: name => name);
            helper.FunctionStarting("hub", "Orchestrator", "instance", "input", FunctionType.Orchestrator, replay);
            helper.FunctionCompleted("hub", "Orchestrator", "instance", "output", false, FunctionType.Orchestrator, replay);
            Assert.Equal(expectedCount, provider.GetAllLogMessages().Count());
            Assert.All(provider.GetAllLogMessages(), message => Assert.True(LogCategories.IsFunctionUserCategory(message.Category)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("Invalid.Function.Name")]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void InvalidFunctionCategory_StillUsesAUserCategory(string? functionName)
        {
            using ILoggerFactory factory = this.CreateLoggerFactory(out TestLoggerProvider provider);
            var helper = new EndToEndTraceHelper(
                factory,
                false,
                shouldTraceRawData: true,
                resolveFunctionName: name => name);
            helper.FunctionCompleted("hub", functionName!, "instance", "SYNTHETIC_CONTENT", false, FunctionType.Activity, false);
            LogMessage message = Assert.Single(provider.GetAllLogMessages());
            Assert.Equal(LogCategories.CreateFunctionUserCategory("DurableTask"), message.Category);
            Assert.Contains("SYNTHETIC_CONTENT", message.FormattedMessage);
            Assert.Empty(provider.CreatedLoggers.Single(logger => logger.Category == TestHelpers.LogCategory).LogMessages);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void UnregisteredFunctionCategories_UseTheSharedFallback()
        {
            using ILoggerFactory factory = this.CreateLoggerFactory(out TestLoggerProvider provider);
            var helper = new EndToEndTraceHelper(
                factory,
                false,
                shouldTraceRawData: true,
                resolveFunctionName: name => name == "KnownFunction" ? name : null);

            helper.FunctionCompleted("hub", "KnownFunction", "instance", "output", false, FunctionType.Activity, false);
            foreach (int index in Enumerable.Range(0, 100))
            {
                helper.FunctionCompleted("hub", $"UnknownFunction{index}", "instance", "output", false, FunctionType.Activity, false);
            }

            TestLogger knownLogger = Assert.Single(
                provider.CreatedLoggers,
                logger => logger.Category == LogCategories.CreateFunctionUserCategory("KnownFunction"));
            TestLogger fallbackLogger = Assert.Single(
                provider.CreatedLoggers,
                logger => logger.Category == LogCategories.CreateFunctionUserCategory("DurableTask"));
            Assert.Single(knownLogger.LogMessages);
            Assert.Equal(100, fallbackLogger.LogMessages.Count);
            Assert.Equal(2, provider.CreatedLoggers.Count(logger => LogCategories.IsFunctionUserCategory(logger.Category)));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void RegistrylessHelper_UsesTheSharedFallback()
        {
            using ILoggerFactory factory = this.CreateLoggerFactory(out TestLoggerProvider provider);
            var helper = new EndToEndTraceHelper(factory, false);

            helper.FunctionCompleted("hub", "ExternalFunction", "instance", "output", false, FunctionType.Activity, false);

            LogMessage message = Assert.Single(provider.GetAllLogMessages());
            Assert.Equal(LogCategories.CreateFunctionUserCategory("DurableTask"), message.Category);
        }

        [Theory]
        [InlineData((int)FunctionType.Orchestrator, false)]
        [InlineData((int)FunctionType.Orchestrator, true)]
        [InlineData((int)FunctionType.Activity, false)]
        [InlineData((int)FunctionType.Activity, true)]
        [InlineData((int)FunctionType.Entity, false)]
        [InlineData((int)FunctionType.Entity, true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void RegisteredFunctions_CasingVariantsShareCanonicalCategory(int functionType, bool disabled)
        {
            const string RegisteredName = "KnownFunction";
            using ILoggerFactory factory = this.CreateLoggerFactory(out TestLoggerProvider provider);
            using DurableTaskExtension extension = CreateExtension(factory);
            FunctionType type = (FunctionType)functionType;
            ITriggeredFunctionExecutor? executor = disabled ? null : Mock.Of<ITriggeredFunctionExecutor>();
            RegisteredFunctionInfo? info = disabled ? null : new RegisteredFunctionInfo(executor!, isOutOfProc: false);
            Action<string> register = type switch
            {
                FunctionType.Orchestrator => name => extension.RegisterOrchestrator(new FunctionName(name), info),
                FunctionType.Activity => name => extension.RegisterActivity(new FunctionName(name), executor!),
                FunctionType.Entity => name => extension.RegisterEntity(new FunctionName(name), info),
                _ => throw new ArgumentOutOfRangeException(nameof(functionType)),
            };

            extension.TraceHelper.FunctionCompleted("hub", RegisteredName, "instance", "output", false, type, false);
            register(RegisteredName);
            register(RegisteredName.ToLowerInvariant());

            for (int index = 0; index < 1024; index++)
            {
                string variant = new string(RegisteredName.Select((character, position) =>
                    position < 10 && (index & (1 << position)) != 0
                        ? char.ToUpperInvariant(character)
                        : char.ToLowerInvariant(character)).ToArray());
                extension.TraceHelper.FunctionCompleted("hub", variant, "instance", "output", false, type, false);
            }

            extension.TraceHelper.FunctionCompleted("hub", "UnknownFunction", "instance", "output", false, type, false);
            TestLogger canonicalLogger = Assert.Single(
                provider.CreatedLoggers,
                logger => logger.Category == LogCategories.CreateFunctionUserCategory(RegisteredName));
            Assert.Equal(1024, canonicalLogger.LogMessages.Count);
            Assert.Equal(
                1024,
                canonicalLogger.LogMessages.Select(message => message.State.Single(property => property.Key == "functionName").Value).Distinct().Count());
            TestLogger fallbackLogger = Assert.Single(
                provider.CreatedLoggers,
                logger => logger.Category == LogCategories.CreateFunctionUserCategory("DurableTask"));
            Assert.Equal(2, fallbackLogger.LogMessages.Count);
            Assert.Equal(2, provider.CreatedLoggers.Count(logger => LogCategories.IsFunctionUserCategory(logger.Category)));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void HostOnlyHelper_CannotEmitFunctionPayloads()
        {
            using ILoggerFactory factory = this.CreateLoggerFactory(out TestLoggerProvider provider);
            var helper = new EndToEndTraceHelper(factory.CreateLogger(TestHelpers.LogCategory), false, shouldTraceRawData: true);
            Assert.Throws<InvalidOperationException>(() =>
                helper.FunctionCompleted("hub", "Activity", "instance", "SYNTHETIC_CONTENT", false, FunctionType.Activity, false));
            Assert.Empty(provider.GetAllLogMessages());
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task ConcurrentFunctions_UseTheirOwnCategories()
        {
            using ILoggerFactory factory = this.CreateLoggerFactory(out TestLoggerProvider provider);
            var helper = new EndToEndTraceHelper(factory, false, resolveFunctionName: name => name);
            await Task.WhenAll(Enumerable.Range(0, 1000).Select(index => Task.Run(() =>
                helper.FunctionCompleted("hub", "Function" + (index % 3), "instance", "output", false, FunctionType.Activity, false))));

            LogMessage[] messages = provider.GetAllLogMessages().ToArray();
            Assert.Equal(1000, messages.Length);
            Assert.All(messages, message =>
                Assert.Equal(
                    LogCategories.CreateFunctionUserCategory((string)message.State.Single(property => property.Key == "functionName").Value),
                    message.Category));
            Assert.Equal(
                messages.Length,
                messages.Select(message => (long)message.State.Single(property => property.Key == "sequenceNumber").Value).Distinct().Count());
            Assert.Equal(3, provider.CreatedLoggers.Count(logger => LogCategories.IsFunctionUserCategory(logger.Category)));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void DebugEntityMessages_UseTheOriginatingFunctionCategory()
        {
            using ILoggerFactory factory = this.CreateLoggerFactory(out TestLoggerProvider provider);
            var helper = new EndToEndTraceHelper(factory, false, resolveFunctionName: name => name);
            helper.DeliveringEntityMessage("EntityFunction", "instance", "execution", 1, "event", "SYNTHETIC_CONTENT");
            helper.SendingEntityMessage("OrchestratorFunction", "instance", "execution", "target", "event", "SYNTHETIC_CONTENT");
#if DEBUG
            LogMessage[] messages = provider.GetAllLogMessages().ToArray();
            Assert.Equal(2, messages.Length);
            Assert.Contains(messages, message => message.Category == LogCategories.CreateFunctionUserCategory("EntityFunction"));
            Assert.Contains(messages, message => message.Category == LogCategories.CreateFunctionUserCategory("OrchestratorFunction"));
            Assert.All(messages, message => Assert.Contains("SYNTHETIC_CONTENT", message.FormattedMessage));
#else
            Assert.Empty(provider.GetAllLogMessages());
#endif
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void DurableTestCollector_MergesCategoriesWithoutApplicationMessages()
        {
            using ILoggerFactory factory = this.CreateLoggerFactory(out TestLoggerProvider provider);
            var helper = new EndToEndTraceHelper(factory, false, resolveFunctionName: name => name);
            helper.FunctionStarting("hub", "Second", "instance", "input", FunctionType.Orchestrator, false);
            factory.CreateLogger(LogCategories.CreateFunctionUserCategory("Second")).LogInformation("Application message for instance");
            helper.FunctionStarting("hub", "First", "instance", "input", FunctionType.Activity, false);
            helper.FunctionCompleted("hub", "Second", "instance", "output", false, FunctionType.Orchestrator, false);

            LogMessage[] messages = TestHelpers.GetDurableLogMessages(provider).ToArray();
            Assert.Equal(3, messages.Length);
            Assert.Equal(
                new[] { "Function.Second.User", "Function.First.User", "Function.Second.User" },
                messages.Select(message => message.Category));
            Assert.DoesNotContain(messages, message => message.FormattedMessage.Contains("Application message", StringComparison.Ordinal));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task UserLogs_EmulatorExecutionPreservesCustomerPayloads()
        {
            const string Payload = "SYNTHETIC_CUSTOMER_PAYLOAD";
            var provider = new TestLoggerProvider(this.output);
            var options = new DurableTaskOptions();
            options.StorageProvider["type"] = "Emulator";
            using ITestHost host = TestHelpers.GetJobHost(
                provider,
                nameof(this.UserLogs_EmulatorExecutionPreservesCustomerPayloads),
                enableExtendedSessions: false,
                storageProviderType: TestHelpers.EmulatorProviderType,
                nameResolver: new SimpleNameResolver(new Dictionary<string, string> { ["FUNCTIONS_WORKER_RUNTIME"] = "dotnet" }),
                options: options,
                types: new[] { typeof(UserLogFunctions), typeof(ClientFunctions) });
            await host.StartAsync();
            try
            {
                TestDurableClient client = await host.StartOrchestratorAsync(nameof(UserLogFunctions.UserLogOrchestrator), Payload, this.output);
                DurableOrchestrationStatus status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(30));
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal(Payload, status.Output.ToObject<string>());
                foreach (string name in new[] { nameof(UserLogFunctions.UserLogOrchestrator), nameof(UserLogFunctions.UserLogActivity) })
                {
                    TestLogger logger = Assert.Single(
                        provider.CreatedLoggers,
                        item => item.Category == LogCategories.CreateFunctionUserCategory(name));
                    Assert.Contains(logger.LogMessages, message => message.FormattedMessage.Contains(Payload, StringComparison.Ordinal));
                }

                TestLogger hostLogger = Assert.Single(provider.CreatedLoggers, logger => logger.Category == TestHelpers.LogCategory);
                Assert.DoesNotContain(hostLogger.LogMessages, message => message.FormattedMessage.Contains(Payload, StringComparison.Ordinal));
                Assert.Contains(hostLogger.LogMessages, message => message.FormattedMessage.Contains("Durable extension configuration loaded", StringComparison.Ordinal));
            }
            finally
            {
                await host.StopAsync();
            }
        }

        private static DurableTaskExtension CreateExtension(ILoggerFactory loggerFactory)
        {
            var options = new OptionsWrapper<DurableTaskOptions>(new DurableTaskOptions
            {
                HubName = "TestHub",
                WebhookUriProviderOverride = () => new Uri("https://localhost"),
            });
            var nameResolver = TestHelpers.GetTestNameResolver();
            var platformInformation = TestHelpers.GetMockPlatformInformationService();
            var storageFactory = new AzureStorageDurabilityProviderFactory(
                options,
                new TestStorageServiceClientProviderFactory(),
                nameResolver,
                loggerFactory,
                platformInformation);
            return new DurableTaskExtension(
                options,
                loggerFactory,
                nameResolver,
                new[] { storageFactory },
                new TestHostShutdownNotificationService(),
                new DurableHttpMessageHandlerFactory(),
                platformInformationService: platformInformation);
        }

        private ILoggerFactory CreateLoggerFactory(out TestLoggerProvider provider)
        {
            var loggerProvider = new TestLoggerProvider(this.output);
            provider = loggerProvider;
            return LoggerFactory.Create(builder => builder
                .SetMinimumLevel(LogLevel.Debug)
                .AddProvider(loggerProvider));
        }

        public static class UserLogFunctions
        {
            [Microsoft.Azure.WebJobs.FunctionName(nameof(UserLogOrchestrator))]
            public static Task<string> UserLogOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                return context.CallActivityAsync<string>(nameof(UserLogActivity), context.GetInput<string>());
            }

            [Microsoft.Azure.WebJobs.FunctionName(nameof(UserLogActivity))]
            public static string UserLogActivity([ActivityTrigger] string input)
            {
                return input;
            }
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
