﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using DurableTask.AzureStorage;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal static class TestHelpers
    {
        public const string LogCategory = "Host.Triggers.DurableTask";

        public static JobHost GetJobHost(
            ILoggerProvider loggerProvider,
            string testName,
            bool enableExtendedSessions,
            string eventGridKeySettingName = null,
            INameResolver nameResolver = null,
            string eventGridTopicEndpoint = null,
            int? eventGridRetryCount = null,
            TimeSpan? eventGridRetryInterval = null,
            int[] eventGridRetryHttpStatus = null,
            bool logReplayEvents = true,
            Uri notificationUrl = null,
            HttpMessageHandler eventGridNotificationHandler = null,
            TimeSpan? maxQueuePollingInterval = null,
            string[] eventGridPublishEventTypes = null,
            bool autoFetchLargeMessages = true)
        {
            var durableTaskOptions = new DurableTaskOptions
            {
                HubName = GetTaskHubNameFromTestName(testName, enableExtendedSessions),
                TraceInputsAndOutputs = true,
                EventGridKeySettingName = eventGridKeySettingName,
                EventGridTopicEndpoint = eventGridTopicEndpoint,
                ExtendedSessionsEnabled = enableExtendedSessions,
                MaxConcurrentOrchestratorFunctions = 200,
                MaxConcurrentActivityFunctions = 200,
                LogReplayEvents = logReplayEvents,
                NotificationUrl = notificationUrl,
                NotificationHandler = eventGridNotificationHandler,
                EventGridPublishEventTypes = eventGridPublishEventTypes,
                FetchLargeMessagesAutomatically = autoFetchLargeMessages,
            };

            if (eventGridRetryCount.HasValue)
            {
                durableTaskOptions.EventGridPublishRetryCount = eventGridRetryCount.Value;
            }

            if (eventGridRetryInterval.HasValue)
            {
                durableTaskOptions.EventGridPublishRetryInterval = eventGridRetryInterval.Value;
            }

            if (eventGridRetryHttpStatus != null)
            {
                durableTaskOptions.EventGridPublishRetryHttpStatus = eventGridRetryHttpStatus;
            }

            if (maxQueuePollingInterval != null)
            {
                durableTaskOptions.MaxQueuePollingInterval = maxQueuePollingInterval.Value;
            }

            var optionsWrapper = new OptionsWrapper<DurableTaskOptions>(durableTaskOptions);
            var testNameResolver = new TestNameResolver(nameResolver);
            return PlatformSpecificHelpers.CreateJobHost(optionsWrapper, loggerProvider, testNameResolver);
        }

        public static string GetTaskHubNameFromTestName(string testName, bool enableExtendedSessions)
        {
            return testName.Replace("_", "") + (enableExtendedSessions ? "EX" : "") + PlatformSpecificHelpers.VersionSuffix;
        }

        public static ITypeLocator GetTypeLocator()
        {
            var types = new Type[]
            {
                typeof(TestOrchestrations),
                typeof(TestActivities),
                typeof(ClientFunctions),
            };

            ITypeLocator typeLocator = new ExplicitTypeLocator(types);
            return typeLocator;
        }

        public static string GetStorageConnectionString()
        {
            return Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        }

        public static Task DeleteTaskHubResources(string testName, bool enableExtendedSessions)
        {
            string hubName = GetTaskHubNameFromTestName(testName, enableExtendedSessions);
            var settings = new AzureStorageOrchestrationServiceSettings
            {
                TaskHubName = hubName,
                StorageConnectionString = GetStorageConnectionString(),
            };

            var service = new AzureStorageOrchestrationService(settings);
            return service.DeleteAsync();
        }

        public static void AssertLogMessageSequence(
            ITestOutputHelper testOutput,
            TestLoggerProvider loggerProvider,
            string testName,
            string instanceId,
            bool filterOutReplayLogs,
            string[] orchestratorFunctionNames,
            string activityFunctionName = null)
        {
            List<string> messageIds;
            string timeStamp;
            var logMessages = GetLogMessages(loggerProvider, testName, instanceId, out messageIds, out timeStamp);

            var expectedLogMessages = GetExpectedLogMessages(
                testName,
                messageIds,
                orchestratorFunctionNames,
                filterOutReplayLogs,
                activityFunctionName,
                timeStamp);
            var actualLogMessages = logMessages.Select(m => m.FormattedMessage).ToList();

            AssertLogMessages(expectedLogMessages, actualLogMessages, testOutput);
        }

        public static void UnhandledOrchesterationExceptionWithRetry_AssertLogMessageSequence(
            ITestOutputHelper testOutput,
            TestLoggerProvider loggerProvider,
            string testName,
            string subOrchestrationInstanceId,
            string[] orchestratorFunctionNames,
            string activityFunctionName = null)
        {
            List<string> messageIds;
            string timeStamp;
            var logMessages = GetLogMessages(loggerProvider, testName, subOrchestrationInstanceId, out messageIds, out timeStamp);

            var actualLogMessages = logMessages.Select(m => m.FormattedMessage).ToList();
            var exceptionCount =
                actualLogMessages.FindAll(m => m.Contains("failed with an error")).Count;

            // The sub-orchestration call is configured to make at most 3 attempts.
            Assert.Equal(3, exceptionCount);
        }

        private static List<LogMessage> GetLogMessages(
            TestLoggerProvider loggerProvider,
            string testName,
            string instanceId,
            out List<string> instanceIds,
            out string timeStamp)
        {
            var logger = loggerProvider.CreatedLoggers.Single(l => l.Category == LogCategory);
            var logMessages = logger.LogMessages.ToList();

            // Remove any logs which may have been generated by concurrently executing orchestrations.
            // Sub-orchestrations are expected to be prefixed with the parent orchestration instance ID.
            logMessages.RemoveAll(msg => !msg.FormattedMessage.Contains(instanceId));

            instanceIds = new List<string> { instanceId };

            timeStamp = string.Empty;

            if (testName.Equals("TimerCancellation", StringComparison.OrdinalIgnoreCase) ||
                testName.Equals("TimerExpiration", StringComparison.OrdinalIgnoreCase))
            {
                // It is assumed that the 4th log message is a timer message.
                timeStamp = GetTimerTimestamp(logMessages[3].FormattedMessage);
            }
            else if (testName.Equals("Orchestration_OnValidOrchestrator", StringComparison.OrdinalIgnoreCase) ||
                     testName.Equals("Orchestration_Activity", StringComparison.OrdinalIgnoreCase))
            {
                // It is assumed that the 5th log message is a sub-orchestration
                instanceIds.Add(GetInstanceId(logMessages[4].FormattedMessage));
            }

            Assert.True(
                logMessages.TrueForAll(m => m.Category.Equals(LogCategory, StringComparison.InvariantCultureIgnoreCase)));

            return logMessages;
        }

        private static IList<string> GetExpectedLogMessages(
            string testName,
            List<string> instanceIds,
            string[] orchestratorFunctionNames,
            bool extendedSessions,
            string activityFunctionName = null,
            string timeStamp = null,
            string[] latencyMs = null)
        {
            var messages = new List<string>();
            switch (testName)
            {
                case "HelloWorldOrchestration_Inline":
                    messages = GetLogs_HelloWorldOrchestration_Inline(instanceIds[0], orchestratorFunctionNames);
                    break;
                case "HelloWorldOrchestration_Activity":
                    messages = GetLogs_HelloWorldOrchestration_Activity(instanceIds[0], orchestratorFunctionNames, activityFunctionName);
                    break;
                case "TerminateOrchestration":
                    messages = GetLogs_TerminateOrchestration(instanceIds[0], orchestratorFunctionNames);
                    break;
                case "TimerCancellation":
                    messages = GetLogs_TimerCancellation(instanceIds[0], orchestratorFunctionNames, timeStamp);
                    break;
                case "TimerExpiration":
                    messages = GetLogs_TimerExpiration(instanceIds[0], orchestratorFunctionNames, timeStamp);
                    break;
                case "UnhandledOrchestrationException":
                    messages = GetLogs_UnhandledOrchestrationException(instanceIds[0], orchestratorFunctionNames);
                    break;
                case "UnhandledActivityException":
                    messages = GetLogs_UnhandledActivityException(instanceIds[0], orchestratorFunctionNames, activityFunctionName);
                    break;
                case "UnhandledActivityExceptionWithRetry":
                    messages = GetLogs_UnhandledActivityExceptionWithRetry(instanceIds[0], orchestratorFunctionNames, activityFunctionName);
                    break;
                case "Orchestration_OnUnregisteredActivity":
                    messages = GetLogs_Orchestration_OnUnregisteredActivity(instanceIds[0], orchestratorFunctionNames);
                    break;
                case "Orchestration_OnUnregisteredOrchestrator":
                    messages = GetLogs_Orchestration_OnUnregisteredOrchestrator(instanceIds[0], orchestratorFunctionNames);
                    break;
                case "Orchestration_OnValidOrchestrator":
                    messages = GetLogs_Orchestration_OnValidOrchestrator(instanceIds.ToArray(), orchestratorFunctionNames, activityFunctionName);
                    break;
                case "Orchestration_Activity":
                    messages = GetLogs_Orchestration_Activity(instanceIds.ToArray(), orchestratorFunctionNames, activityFunctionName);
                    break;
                case "OrchestrationEventGridApiReturnBadStatus":
                    messages = GetLogs_OrchestrationEventGridApiReturnBadStatus(instanceIds[0], orchestratorFunctionNames, latencyMs);
                    break;
                case "RewindOrchestration":
                    messages = GetLogs_Rewind_Orchestration(instanceIds[0], orchestratorFunctionNames, activityFunctionName);
                    break;
                case nameof(DurableTaskEndToEndTests.ActorOrchestration):
                    messages = GetLogs_ActorOrchestration(instanceIds[0]).ToList();
                    break;
                default:
                    break;
            }

            // Remove any logs which may have been generated by concurrently executing orchestrations
            messages.RemoveAll(str => !instanceIds.Any(id => str.Contains(id)));

            if (extendedSessions)
            {
                // Remove replay logs - those are never expected for these tests.
                messages.RemoveAll(str => str.Contains("IsReplay: True"));
            }

            return messages;
        }

        private static void AssertLogMessages(IList<string> expected, IList<string> actual, ITestOutputHelper testOutput)
        {
            TraceExpectedLogMessages(testOutput, expected);

            Assert.Equal(expected.Count, actual.Count);

            for (int i = 0; i < expected.Count; i++)
            {
                Assert.StartsWith(expected[i], actual[i]);
            }
        }

        private static void TraceExpectedLogMessages(ITestOutputHelper testOutput, IList<string> expected)
        {
            string prefix = "    ";
            string allExpectedTraces = string.Join(Environment.NewLine + prefix, expected);
            testOutput.WriteLine("Expected trace output:");
            testOutput.WriteLine(prefix + allExpectedTraces);
        }

        private static List<string> GetLogs_HelloWorldOrchestration_Inline(string instanceId, string[] functionNames)
        {
            var list = new List<string>()
            {
                $"{instanceId}: Function '{functionNames[0]} ({FunctionType.Orchestrator})' scheduled. Reason: NewInstance. IsReplay: False.",
                $"{instanceId}: Function '{functionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: False. Input: \"World\"",
                $"{instanceId}: Function '{functionNames[0]} ({FunctionType.Orchestrator})' completed. ContinuedAsNew: False. IsReplay: False. Output: \"Hello, World!\"",
            };

            return list;
        }

        private static List<string> GetLogs_OrchestrationEventGridApiReturnBadStatus(string messageId, string[] functionNames, string[] latencyMs)
        {
            var list = new List<string>()
            {
                $"{messageId}: Function '{functionNames[0]} ({FunctionType.Orchestrator})' scheduled. Reason: NewInstance. IsReplay: False. State: Scheduled.",
                $"{messageId}: Function '{functionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: False. Input: \"World\". State: Started.",
                $"{messageId}: Function '{functionNames[0]} ({FunctionType.Orchestrator})' completed. ContinuedAsNew: False. IsReplay: False. Output: \"Hello, World!\". State: Completed.",
                $"{messageId}: Function '{functionNames[0]} ({FunctionType.Orchestrator})' failed to send a 'Started' notification event to Azure Event Grid. Status code: 500. Details: {{\"message\":\"Exception has been thrown\"}}. ",
                $"{messageId}: Function '{functionNames[0]} ({FunctionType.Orchestrator})' failed to send a 'Completed' notification event to Azure Event Grid. Status code: 500. Details: {{\"message\":\"Exception has been thrown\"}}. ",
            };
            return list;
        }

        private static List<string> GetLogs_HelloWorldOrchestration_Activity(string messageId, string[] orchestratorFunctionNames, string activityFunctionName)
        {
            var list = new List<string>
            {
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' scheduled. Reason: NewInstance. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: False. Input: \"World\"",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})' scheduled. Reason: {orchestratorFunctionNames[0]}. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' awaited. IsReplay: False.",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})' started. IsReplay: False. Input: [\"World\"]",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})' completed. ContinuedAsNew: False. IsReplay: False. Output: \"Hello, World!\"",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: True. Input: \"World\"",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})' scheduled. Reason: {orchestratorFunctionNames[0]}. IsReplay: True.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' completed. ContinuedAsNew: False. IsReplay: False. Output: \"Hello, World!\"",
            };

            return list;
        }

        private static List<string> GetLogs_TerminateOrchestration(string messageId, string[] orchestratorFunctionNames)
        {
            var list = new List<string>()
            {
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' scheduled. Reason: NewInstance. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: False. Input: 0",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' is waiting for input. Reason: WaitForExternalEvent:operation. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' awaited. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' was terminated. Reason: sayōnara",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: True. Input: 0",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' is waiting for input. Reason: WaitForExternalEvent:operation. IsReplay: True.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' awaited. IsReplay: False.",
            };

            return list;
        }

        private static List<string> GetLogs_TimerCancellation(string messageId, string[] orchestratorFunctionNames, string timerTimestamp)
        {
            var list = new List<string>()
            {
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' scheduled. Reason: NewInstance. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: False. Input: \"00:00:10\"",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' is waiting for input. Reason: WaitForExternalEvent:approval. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' is waiting for input. Reason: CreateTimer:{timerTimestamp}. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' awaited. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' scheduled. Reason: RaiseEvent:approval. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: True. Input: \"00:00:10\"",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' is waiting for input. Reason: WaitForExternalEvent:approval. IsReplay: True.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' is waiting for input. Reason: CreateTimer:{timerTimestamp}. IsReplay: True.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' received a 'approval' event.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' completed. ContinuedAsNew: False. IsReplay: False. Output: \"Approved\"",
            };

            return list;
        }

        private static List<string> GetLogs_TimerExpiration(string messageId, string[] orchestratorFunctionNames, string timerTimestamp)
        {
            var list = new List<string>()
            {
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' scheduled. Reason: NewInstance. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: False. Input: \"00:00:10\"",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' is waiting for input. Reason: WaitForExternalEvent:approval. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' is waiting for input. Reason: CreateTimer:{timerTimestamp}. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' awaited. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: True. Input: \"00:00:10\"",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' is waiting for input. Reason: WaitForExternalEvent:approval. IsReplay: True.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' is waiting for input. Reason: CreateTimer:{timerTimestamp}. IsReplay: True.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' was resumed by a timer scheduled for '{timerTimestamp}'. IsReplay: False. State: TimerExpired",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' completed. ContinuedAsNew: False. IsReplay: False. Output: \"Expired\"",
            };

            return list;
        }

        private static List<string> GetLogs_UnhandledOrchestrationException(string messageId, string[] orchestratorFunctionNames)
        {
            var list = new List<string>()
            {
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' scheduled. Reason: NewInstance. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: False. Input: null",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' failed with an error. Reason: System.ArgumentNullException: Value cannot be null.",
            };

            return list;
        }

        private static List<string> GetLogs_UnhandledActivityException(string messageId, string[] orchestratorFunctionNames, string activityFunctionName)
        {
            var list = new List<string>()
            {
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' scheduled. Reason: NewInstance. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: False. Input: \"Kah-BOOOOM!!!\"",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})' scheduled. Reason: ThrowOrchestrator. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' awaited. IsReplay: False.",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})' started. IsReplay: False. Input: [\"Kah-BOOOOM!!!\"]",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})' failed with an error. Reason: System.InvalidOperationException: Kah-BOOOOM!!!",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: True. Input: \"Kah-BOOOOM!!!\"",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})' scheduled. Reason: ThrowOrchestrator. IsReplay: True.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' failed with an error. Reason: Microsoft.Azure.WebJobs.FunctionFailedException: The activity function 'ThrowActivity' failed: \"Kah-BOOOOM!!!\"",
            };

            return list;
        }

        private static List<string> GetLogs_UnhandledActivityExceptionWithRetry(string messageId, string[] orchestratorFunctionNames, string activityFunctionName)
        {
            var list = new List<string>()
            {
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' scheduled. Reason: NewInstance. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: False. Input: \"Kah-BOOOOM!!!\"",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})' scheduled. Reason: ActivityThrowWithRetry. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' awaited. IsReplay: False.",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})' started. IsReplay: False. Input: [\"Kah-BOOOOM!!!\"]",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})' failed with an error. Reason: System.InvalidOperationException: Kah-BOOOOM!!!",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: True. Input: \"Kah-BOOOOM!!!\"",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})' scheduled. Reason: ActivityThrowWithRetry. IsReplay: True.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' awaited. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: True. Input: \"Kah-BOOOOM!!!\"",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})' scheduled. Reason: ActivityThrowWithRetry. IsReplay: True.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' awaited. IsReplay: False.",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})' started. IsReplay: False. Input: [\"Kah-BOOOOM!!!\"]",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})' failed with an error. Reason: System.InvalidOperationException: Kah-BOOOOM!!!",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: True. Input: \"Kah-BOOOOM!!!\"",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})' scheduled. Reason: ActivityThrowWithRetry. IsReplay: True.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' awaited. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: True.",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})' scheduled. Reason: ActivityThrowWithRetry. IsReplay: True.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' awaited. IsReplay: False.",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})' started. IsReplay: False. Input: [\"Kah-BOOOOM!!!\"]",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})' failed with an error. Reason: System.InvalidOperationException: Kah-BOOOOM!!!",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: True. Input: \"Kah-BOOOOM!!!\"",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})' scheduled. Reason: ActivityThrowWithRetry. IsReplay: True.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' awaited. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: True. Input: \"Kah-BOOOOM!!!\"",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})' scheduled. Reason: ActivityThrowWithRetry. IsReplay: True.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' failed with an error. Reason: Microsoft.Azure.WebJobs.FunctionFailedException: The activity function 'ThrowActivity' failed: \"Kah-BOOOOM!!!\"",
            };

            return list;
        }

        private static List<string> GetLogs_Orchestration_OnUnregisteredActivity(string messageId, string[] orchestratorFunctionNames)
        {
            var list = new List<string>()
            {
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' scheduled. Reason: NewInstance. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' failed with an error. Reason: System.ArgumentException: The function 'UnregisteredActivity' doesn't exist, is disabled, or is not an activity function.",
            };

            return list;
        }

        private static List<string> GetLogs_Orchestration_OnUnregisteredOrchestrator(string messageId, string[] orchestratorFunctionNames)
        {
            var list = new List<string>()
            {
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' scheduled. Reason: NewInstance. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' failed with an error. Reason: System.ArgumentException: The function 'UnregisteredOrchestrator' doesn't exist, is disabled, or is not an orchestrator function.",
            };

            return list;
        }

        private static List<string> GetLogs_Orchestration_OnValidOrchestrator(string[] messageIds, string[] orchestratorFunctionNames, string activityFunctionName)
        {
            var list = new List<string>()
            {
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' scheduled. Reason: NewInstance. IsReplay: False.",
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: False.",
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[1]} ({FunctionType.Orchestrator})' scheduled. Reason: CallOrchestrator. IsReplay: False.",
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' awaited. IsReplay: False.",
                $"{messageIds[1]}:0: Function '{orchestratorFunctionNames[1]} ({FunctionType.Orchestrator})' started. IsReplay: False.",
                $"{messageIds[1]}:0: Function '{activityFunctionName} ({FunctionType.Activity})' scheduled. Reason: SayHelloWithActivity. IsReplay: False.",
                $"{messageIds[1]}:0: Function '{orchestratorFunctionNames[1]} ({FunctionType.Orchestrator})' awaited. IsReplay: False.",
                $"{messageIds[1]}:0: Function '{activityFunctionName} ({FunctionType.Activity})' started. IsReplay: False.",
                $"{messageIds[1]}:0: Function '{activityFunctionName} ({FunctionType.Activity})' completed. ContinuedAsNew: False. IsReplay: False. Output: \"Hello, ",
                $"{messageIds[1]}:0: Function '{orchestratorFunctionNames[1]} ({FunctionType.Orchestrator})' started. IsReplay: True.",
                $"{messageIds[1]}:0: Function '{activityFunctionName} ({FunctionType.Activity})' scheduled. Reason: SayHelloWithActivity. IsReplay: True.",
                $"{messageIds[1]}:0: Function '{orchestratorFunctionNames[1]} ({FunctionType.Orchestrator})' completed. ContinuedAsNew: False. IsReplay: False. Output: \"Hello,",
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: True.",
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[1]} ({FunctionType.Orchestrator})' scheduled. Reason: CallOrchestrator. IsReplay: True.",
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' completed. ContinuedAsNew: False. IsReplay: False. Output: \"Hello,",
            };

            return list;
        }

        private static List<string> GetLogs_Orchestration_Activity(string[] messageIds, string[] orchestratorFunctionNames, string activityFunctionName)
        {
            var list = new List<string>()
            {
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' scheduled. Reason: NewInstance. IsReplay: False.",
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: False.",
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[1]} ({FunctionType.Orchestrator})' scheduled. Reason: OrchestratorGreeting. IsReplay: False.",
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' awaited. IsReplay: False.",
                $"{messageIds[1]}:0: Function '{orchestratorFunctionNames[1]} ({FunctionType.Orchestrator})' started. IsReplay: False.",
                $"{messageIds[1]}:0: Function '{activityFunctionName} ({FunctionType.Activity})' scheduled. Reason: SayHelloWithActivity. IsReplay: False.",
                $"{messageIds[1]}:0: Function '{orchestratorFunctionNames[1]} ({FunctionType.Orchestrator})' awaited. IsReplay: False.",
                $"{messageIds[1]}:0: Function '{activityFunctionName} ({FunctionType.Activity})' started. IsReplay: False.",
                $"{messageIds[1]}:0: Function '{activityFunctionName} ({FunctionType.Activity})' completed. ContinuedAsNew: False. IsReplay: False. Output: \"Hello, ",
                $"{messageIds[1]}:0: Function '{orchestratorFunctionNames[1]} ({FunctionType.Orchestrator})' started. IsReplay: True.",
                $"{messageIds[1]}:0: Function '{activityFunctionName} ({FunctionType.Activity})' scheduled. Reason: SayHelloWithActivity. IsReplay: True.",
                $"{messageIds[1]}:0: Function '{orchestratorFunctionNames[1]} ({FunctionType.Orchestrator})' completed. ContinuedAsNew: False. IsReplay: False. Output: \"Hello,",
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: True.",
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[1]} ({FunctionType.Orchestrator})' scheduled. Reason: OrchestratorGreeting. IsReplay: True.",
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' completed. ContinuedAsNew: False. IsReplay: False. Output: (null)",
            };

            return list;
        }

        private static List<string> GetLogs_Rewind_Orchestration(string messageId, string[] orchestratorFunctionNames, string activityFunctionName)
        {
            var list = new List<string>()
            {
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' scheduled. Reason: NewInstance. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: False.",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})' scheduled. Reason: SayHelloWithActivityForRewind. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' awaited. IsReplay: False.",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})' started. IsReplay: False.",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})' completed. ContinuedAsNew: False. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: True.",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})' scheduled. Reason: SayHelloWithActivityForRewind. IsReplay: True.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' failed with an error. Reason: System.Exception: Simulating Orchestration failure.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' was rewound. Reason: rewind!. State: Rewound.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' started. IsReplay: True.",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})' scheduled. Reason: SayHelloWithActivityForRewind. IsReplay: True.",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})' completed. ContinuedAsNew: False. IsReplay: True. Output: (replayed).",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})' completed. ContinuedAsNew: False. IsReplay: True. Output: \"Hello, Catherine!\". State: Completed.",
            };

            return list;
        }

        private static string[] GetLogs_ActorOrchestration(string instanceId)
        {
            return new[]
            {
                $"{instanceId}: Function 'Counter (Orchestrator)' scheduled. Reason: NewInstance. IsReplay: False. State: Scheduled.",
                $"{instanceId}: Function 'Counter (Orchestrator)' started. IsReplay: False. Input: 0. State: Started.",
                $"{instanceId}: Function 'Counter (Orchestrator)' is waiting for input. Reason: WaitForExternalEvent:operation. IsReplay: False. State: Listening.",
                $"{instanceId}: Function 'Counter (Orchestrator)' awaited. IsReplay: False. State: Awaited.",
                $"{instanceId}: Function 'Counter (Orchestrator)' scheduled. Reason: RaiseEvent:operation. IsReplay: False. State: Scheduled.",
                $"{instanceId}: Function 'Counter (Orchestrator)' started. IsReplay: True. Input: 0. State: Started.",
                $"{instanceId}: Function 'Counter (Orchestrator)' is waiting for input. Reason: WaitForExternalEvent:operation. IsReplay: True. State: Listening.",
                $"{instanceId}: Function 'Counter (Orchestrator)' received a 'operation' event. State: ExternalEventRaised.",
                $"{instanceId}: Function 'Counter (Orchestrator)' completed. ContinuedAsNew: True. IsReplay: False. Output: 1. State: Completed.",
                $"{instanceId}: Function 'Counter (Orchestrator)' started. IsReplay: False. Input: 1. State: Started.",
                $"{instanceId}: Function 'Counter (Orchestrator)' is waiting for input. Reason: WaitForExternalEvent:operation. IsReplay: False. State: Listening.",
                $"{instanceId}: Function 'Counter (Orchestrator)' awaited. IsReplay: False. State: Awaited.",
                $"{instanceId}: Function 'Counter (Orchestrator)' scheduled. Reason: RaiseEvent:operation. IsReplay: False. State: Scheduled.",
                $"{instanceId}: Function 'Counter (Orchestrator)' started. IsReplay: True. Input: 1. State: Started.",
                $"{instanceId}: Function 'Counter (Orchestrator)' is waiting for input. Reason: WaitForExternalEvent:operation. IsReplay: True. State: Listening.",
                $"{instanceId}: Function 'Counter (Orchestrator)' received a 'operation' event. State: ExternalEventRaised.",
                $"{instanceId}: Function 'Counter (Orchestrator)' completed. ContinuedAsNew: True. IsReplay: False. Output: 2. State: Completed.",
                $"{instanceId}: Function 'Counter (Orchestrator)' started. IsReplay: False. Input: 2. State: Started.",
                $"{instanceId}: Function 'Counter (Orchestrator)' is waiting for input. Reason: WaitForExternalEvent:operation. IsReplay: False. State: Listening.",
                $"{instanceId}: Function 'Counter (Orchestrator)' awaited. IsReplay: False. State: Awaited.",
                $"{instanceId}: Function 'Counter (Orchestrator)' scheduled. Reason: RaiseEvent:operation. IsReplay: False. State: Scheduled.",
                $"{instanceId}: Function 'Counter (Orchestrator)' started. IsReplay: True. Input: 2. State: Started.",
                $"{instanceId}: Function 'Counter (Orchestrator)' is waiting for input. Reason: WaitForExternalEvent:operation. IsReplay: True. State: Listening.",
                $"{instanceId}: Function 'Counter (Orchestrator)' received a 'operation' event. State: ExternalEventRaised.",
                $"{instanceId}: Function 'Counter (Orchestrator)' completed. ContinuedAsNew: True. IsReplay: False. Output: 3. State: Completed.",
                $"{instanceId}: Function 'Counter (Orchestrator)' started. IsReplay: False. Input: 3. State: Started.",
                $"{instanceId}: Function 'Counter (Orchestrator)' is waiting for input. Reason: WaitForExternalEvent:operation. IsReplay: False. State: Listening.",
                $"{instanceId}: Function 'Counter (Orchestrator)' awaited. IsReplay: False. State: Awaited.",
                $"{instanceId}: Function 'Counter (Orchestrator)' scheduled. Reason: RaiseEvent:operation. IsReplay: False. State: Scheduled.",
                $"{instanceId}: Function 'Counter (Orchestrator)' started. IsReplay: True. Input: 3. State: Started.",
                $"{instanceId}: Function 'Counter (Orchestrator)' is waiting for input. Reason: WaitForExternalEvent:operation. IsReplay: True. State: Listening.",
                $"{instanceId}: Function 'Counter (Orchestrator)' received a 'operation' event. State: ExternalEventRaised.",
                $"{instanceId}: Function 'Counter (Orchestrator)' completed. ContinuedAsNew: True. IsReplay: False. Output: 2. State: Completed.",
                $"{instanceId}: Function 'Counter (Orchestrator)' started. IsReplay: False. Input: 2. State: Started.",
                $"{instanceId}: Function 'Counter (Orchestrator)' is waiting for input. Reason: WaitForExternalEvent:operation. IsReplay: False. State: Listening.",
                $"{instanceId}: Function 'Counter (Orchestrator)' awaited. IsReplay: False. State: Awaited.",
                $"{instanceId}: Function 'Counter (Orchestrator)' scheduled. Reason: RaiseEvent:operation. IsReplay: False. State: Scheduled.",
                $"{instanceId}: Function 'Counter (Orchestrator)' started. IsReplay: True. Input: 2. State: Started.",
                $"{instanceId}: Function 'Counter (Orchestrator)' is waiting for input. Reason: WaitForExternalEvent:operation. IsReplay: True. State: Listening.",
                $"{instanceId}: Function 'Counter (Orchestrator)' received a 'operation' event. State: ExternalEventRaised.",
                $"{instanceId}: Function 'Counter (Orchestrator)' completed. ContinuedAsNew: True. IsReplay: False. Output: 3. State: Completed.",
                $"{instanceId}: Function 'Counter (Orchestrator)' started. IsReplay: False. Input: 3. State: Started.",
                $"{instanceId}: Function 'Counter (Orchestrator)' is waiting for input. Reason: WaitForExternalEvent:operation. IsReplay: False. State: Listening.",
                $"{instanceId}: Function 'Counter (Orchestrator)' awaited. IsReplay: False. State: Awaited.",
                $"{instanceId}: Function 'Counter (Orchestrator)' scheduled. Reason: RaiseEvent:operation. IsReplay: False. State: Scheduled.",
                $"{instanceId}: Function 'Counter (Orchestrator)' started. IsReplay: True. Input: 3. State: Started.",
                $"{instanceId}: Function 'Counter (Orchestrator)' is waiting for input. Reason: WaitForExternalEvent:operation. IsReplay: True. State: Listening.",
                $"{instanceId}: Function 'Counter (Orchestrator)' received a 'operation' event. State: ExternalEventRaised.",
                $"{instanceId}: Function 'Counter (Orchestrator)' completed. ContinuedAsNew: False. IsReplay: False. Output: 3. State: Completed.",
            };
        }

        private static string GetInstanceId(string message)
        {
            return message.Substring(0, message.IndexOf(':'));
        }

        private static string GetTimerTimestamp(string message)
        {
            const string CreateTimerPrefix = "CreateTimer:";
            int start = message.IndexOf(CreateTimerPrefix) + CreateTimerPrefix.Length;
            int end = message.IndexOf('Z', start) + 1;
            return message.Substring(start, end - start);
        }

        internal static INameResolver GetTestNameResolver()
        {
            return new TestNameResolver(null);
        }

        private class ExplicitTypeLocator : ITypeLocator
        {
            private readonly IReadOnlyList<Type> types;

            public ExplicitTypeLocator(params Type[] types)
            {
                this.types = types.ToList().AsReadOnly();
            }

            public IReadOnlyList<Type> GetTypes()
            {
                return this.types;
            }
        }

        private class TestNameResolver : INameResolver
        {
            private static readonly Dictionary<string, string> DefaultAppSettings = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                { "TestTaskHub", string.Empty },
            };

            private readonly INameResolver innerResolver;

            public TestNameResolver(INameResolver innerResolver)
            {
                // null is okay
                this.innerResolver = innerResolver;
            }

            public string Resolve(string name)
            {
                if (string.IsNullOrEmpty(name))
                {
                    return null;
                }

                string value = this.innerResolver?.Resolve(name);
                if (value == null)
                {
                    DefaultAppSettings.TryGetValue(name, out value);
                }

                if (value == null)
                {
                    value = Environment.GetEnvironmentVariable(name);
                }

                return value;
            }
        }
    }
}
