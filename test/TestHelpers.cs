// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal static class TestHelpers
    {
        public const string LogCategory = "Host.Triggers.DurableTask";
        private static int eventGridErrorCounter;
        
        public static JobHost GetJobHost(ILoggerFactory loggerFactory, string taskHub = "CommonTestHub", string eventGridKeySettingName = null, string eventGridKeyValue = null, string eventGridTopicEndpoint = null)
        {
            var config = new JobHostConfiguration { HostId = "durable-task-host" };
            config.ConfigureDurableFunctionTypeLocator(typeof(TestOrchestrations), typeof(TestActivities));
            config.UseDurableTask(new DurableTaskExtension
            {
                HubName = taskHub.Replace("_", ""),
                TraceInputsAndOutputs = true,
                EventGridKeySettingName = eventGridKeySettingName,
                EventGridTopicEndpoint = eventGridTopicEndpoint,
            });

            // Mock INameResolver for not setting EnvironmentVariables.
            if (eventGridKeyValue != null)
            {
                config.AddService<INameResolver>(new MockNameResolver(eventGridKeyValue));
            }

            // Performance is *significantly* worse when dashboard logging is enabled, at least
            // when running in the storage emulator. Disabling to keep tests running quickly.
            config.DashboardConnectionString = null;

            // Add test logger
            config.LoggerFactory = loggerFactory;

            var host = new JobHost(config);
            return host;
        }

        public static void AssertLogMessageSequence(
            ITestOutputHelper testOutput,
            TestLoggerProvider loggerProvider,
            string testName,
            string[] orchestratorFunctionNames,
            string activityFunctionName = null)
        {
            List<string> messageIds;
            string timeStamp;
            var logMessages = GetLogMessages(loggerProvider, testName, out messageIds, out timeStamp);

            var expectedLogMessages = GetExpectedLogMessages(testName, messageIds, orchestratorFunctionNames, activityFunctionName, timeStamp);
            var actualLogMessages = logMessages.Select(m => m.FormattedMessage).ToList();

            AssertLogMessages(expectedLogMessages, actualLogMessages, testOutput);
        }

        public static void UnhandledOrchesterationExceptionWithRetry_AssertLogMessageSequence(
            ITestOutputHelper testOutput,
            TestLoggerProvider loggerProvider,
            string testName,
            string[] orchestratorFunctionNames,
            string activityFunctionName = null)
        {
            List<string> messageIds;
            string timeStamp;
            string[] latencyMs;
            var logMessages = GetLogMessages(loggerProvider, testName, out messageIds, out timeStamp);

            var actualLogMessages = logMessages.Select(m => m.FormattedMessage).ToList();
            var exceptionCount =
                actualLogMessages.FindAll(m => m.Contains("failed with an error")).Count;

            Assert.Equal(4, exceptionCount);
        }

        private static List<LogMessage> GetLogMessages(
            TestLoggerProvider loggerProvider,
            string testName,
            out List<string> messageIds,
            out string timeStamp)
        {
            var logger = loggerProvider.CreatedLoggers.Single(l => l.Category == LogCategory);
            var logMessages = logger.LogMessages.ToList();
            messageIds = new List<string>()
            {
                GetMessageId(logMessages[0].FormattedMessage),
            };

            timeStamp = string.Empty;

            if (testName.Equals("TimerCancellation", StringComparison.InvariantCultureIgnoreCase)
                || testName.Equals("TimerExpiration", StringComparison.InvariantCultureIgnoreCase))
            {
                timeStamp = GetTimerTimestamp(logMessages[3].FormattedMessage);
            }
            else if (testName.Equals("Orchestration_OnValidOrchestrator", StringComparison.InvariantCultureIgnoreCase) ||
                     testName.Equals("Orchestration_Activity", StringComparison.InvariantCultureIgnoreCase))
            {
                messageIds.Add(GetMessageId(logMessages[4].FormattedMessage));
            }

            Assert.True(
                logMessages.TrueForAll(m => m.Category.Equals(LogCategory, StringComparison.InvariantCultureIgnoreCase)));
            return logMessages;
        }

        private static IList<string> GetExpectedLogMessages(string testName, List<string> messageIds, string[] orchestratorFunctionNames, string activityFunctionName = null, string timeStamp = null, string[] latencyMs = null)
        {
            var messages = new List<string>();
            switch (testName)
            {
                case "HelloWorldOrchestration_Inline":
                    messages = GetLogs_HelloWorldOrchestration_Inline(messageIds[0], orchestratorFunctionNames);
                    break;
                case "HelloWorldOrchestration_Activity":
                    messages = GetLogs_HelloWorldOrchestration_Activity(messageIds[0], orchestratorFunctionNames, activityFunctionName);
                    break;
                case "TerminateOrchestration":
                    messages = GetLogs_TerminateOrchestration(messageIds[0], orchestratorFunctionNames);
                    break;
                case "TimerCancellation":
                    messages = GetLogs_TimerCancellation(messageIds[0], orchestratorFunctionNames, timeStamp);
                    break;
                case "TimerExpiration":
                    messages = GetLogs_TimerExpiration(messageIds[0], orchestratorFunctionNames, timeStamp);
                    break;
                case "UnhandledOrchestrationException":
                    messages = GetLogs_UnhandledOrchestrationException(messageIds[0], orchestratorFunctionNames);
                    break;
                case "UnhandledActivityException":
                    messages = GetLogs_UnhandledActivityException(messageIds[0], orchestratorFunctionNames, activityFunctionName);
                    break;
                case "UnhandledActivityExceptionWithRetry":
                    messages = GetLogs_UnhandledActivityExceptionWithRetry(messageIds[0], orchestratorFunctionNames, activityFunctionName);
                    break;
                case "Orchestration_OnUnregisteredActivity":
                    messages = GetLogs_Orchestration_OnUnregisteredActivity(messageIds[0], orchestratorFunctionNames);
                    break;
                case "Orchestration_OnUnregisteredOrchestrator":
                    messages = GetLogs_Orchestration_OnUnregisteredOrchestrator(messageIds[0], orchestratorFunctionNames);
                    break;
                case "Orchestration_OnValidOrchestrator":
                    messages = GetLogs_Orchestration_OnValidOrchestrator(messageIds.ToArray(), orchestratorFunctionNames, activityFunctionName);
                    break;
                case "Orchestration_Activity":
                    messages = GetLogs_Orchestration_Activity(messageIds.ToArray(), orchestratorFunctionNames, activityFunctionName);
                    break;
                case "OrchestrationEventGridApiReturnBadStatus":
                    messages = GetLogs_OrchestrationEventGridApiReturnBadStatus(messageIds[0], orchestratorFunctionNames, latencyMs);
                    break;
                default:
                    break;
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

        private static List<string> GetLogs_HelloWorldOrchestration_Inline(string messageId, string[] functionNames)
        {
            var list = new List<string>()
            {
                $"{messageId}: Function '{functionNames[0]} ({FunctionType.Orchestrator})', version '' scheduled. Reason: NewInstance. IsReplay: False.",
                $"{messageId}: Function '{functionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: False. Input: \"World\"",
                $"{messageId}: Function '{functionNames[0]} ({FunctionType.Orchestrator})', version '' completed. ContinuedAsNew: False. IsReplay: False. Output: \"Hello, World!\"",
            };

            return list;
        }

        private static List<string> GetLogs_OrchestrationEventGridApiReturnBadStatus(string messageId, string[] functionNames, string[] latencyMs)
        {
            var list = new List<string>()
            {
                $"{messageId}: Function '{functionNames[0]} ({FunctionType.Orchestrator})', version '' scheduled. Reason: NewInstance. IsReplay: False. State: Scheduled. HubName: OrchestrationStartAndCompleted. AppName: . SlotName: . ExtensionVersion: 1.2.1.0. SequenceNumber: 0.",
                $"{messageId}: Function '{functionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: False. Input: \"World\". State: Started. HubName: OrchestrationStartAndCompleted. AppName: . SlotName: . ExtensionVersion: 1.2.1.0. SequenceNumber: 1.",
                $"{messageId}: Function '{functionNames[0]} ({FunctionType.Orchestrator})', version '' completed. ContinuedAsNew: False. IsReplay: False. Output: \"Hello, World!\". State: Completed. HubName: OrchestrationStartAndCompleted. AppName: . SlotName: . ExtensionVersion: 1.2.1.0. SequenceNumber: 2.",
                $"{messageId}: Function '{functionNames[0]} ({FunctionType.Orchestrator})', failed to send a 'Started' notification event to Azure Event Grid. Status code: InternalServerError. Details: {{\"message\":\"Exception has been thrown\"}}. ",
                $"{messageId}: Function '{functionNames[0]} ({FunctionType.Orchestrator})', failed to send a 'Completed' notification event to Azure Event Grid. Status code: InternalServerError. Details: {{\"message\":\"Exception has been thrown\"}}. ",
            };
            return list;
        }

        private static List<string> GetLogs_HelloWorldOrchestration_Activity(string messageId, string[] orchestratorFunctionNames, string activityFunctionName)
        {
            var list = new List<string>()
            {
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' scheduled. Reason: NewInstance. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: False. Input: \"World\"",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})', version '' scheduled. Reason: {orchestratorFunctionNames[0]}. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' awaited. IsReplay: False.",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})', version '' started. IsReplay: False. Input: [\"World\"]",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})', version '' completed. ContinuedAsNew: False. IsReplay: False. Output: \"Hello, World!\"",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: True. Input: \"World\"",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})', version '' scheduled. Reason: {orchestratorFunctionNames[0]}. IsReplay: True.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' completed. ContinuedAsNew: False. IsReplay: False. Output: \"Hello, World!\"",
            };

            return list;
        }

        private static List<string> GetLogs_TerminateOrchestration(string messageId, string[] orchestratorFunctionNames)
        {
            var list = new List<string>()
            {
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' scheduled. Reason: NewInstance. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: False. Input: 0",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' is waiting for input. Reason: WaitForExternalEvent:operation. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' awaited. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' was terminated. Reason: sayōnara",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: True. Input: 0",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' is waiting for input. Reason: WaitForExternalEvent:operation. IsReplay: True.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' awaited. IsReplay: False.",
            };

            return list;
        }

        private static List<string> GetLogs_TimerCancellation(string messageId, string[] orchestratorFunctionNames, string timerTimestamp)
        {
            var list = new List<string>()
            {
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' scheduled. Reason: NewInstance. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: False. Input: \"00:00:10\"",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' is waiting for input. Reason: WaitForExternalEvent:approval. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' is waiting for input. Reason: CreateTimer:{timerTimestamp}. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' awaited. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' scheduled. Reason: RaiseEvent:approval. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: True. Input: \"00:00:10\"",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' is waiting for input. Reason: WaitForExternalEvent:approval. IsReplay: True.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' is waiting for input. Reason: CreateTimer:{timerTimestamp}. IsReplay: True.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' received a 'approval' event.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' completed. ContinuedAsNew: False. IsReplay: False. Output: \"Approved\"",
            };

            return list;
        }

        private static List<string> GetLogs_TimerExpiration(string messageId, string[] orchestratorFunctionNames, string timerTimestamp)
        {
            var list = new List<string>()
            {
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' scheduled. Reason: NewInstance. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: False. Input: \"00:00:10\"",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' is waiting for input. Reason: WaitForExternalEvent:approval. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' is waiting for input. Reason: CreateTimer:{timerTimestamp}. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' awaited. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: True. Input: \"00:00:10\"",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' is waiting for input. Reason: WaitForExternalEvent:approval. IsReplay: True.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' is waiting for input. Reason: CreateTimer:{timerTimestamp}. IsReplay: True.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' was resumed by a timer scheduled for '{timerTimestamp}'. IsReplay: False. State: TimerExpired",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' completed. ContinuedAsNew: False. IsReplay: False. Output: \"Expired\"",
            };

            return list;
        }

        private static List<string> GetLogs_UnhandledOrchestrationException(string messageId, string[] orchestratorFunctionNames)
        {
            var list = new List<string>()
            {
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' scheduled. Reason: NewInstance. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: False. Input: null",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' failed with an error. Reason: System.ArgumentNullException: Value cannot be null.",
            };

            return list;
        }

        private static List<string> GetLogs_UnhandledActivityException(string messageId, string[] orchestratorFunctionNames, string activityFunctionName)
        {
            var list = new List<string>()
            {
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' scheduled. Reason: NewInstance. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: False. Input: \"Kah-BOOOOM!!!\"",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})', version '' scheduled. Reason: Throw. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' awaited. IsReplay: False.",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})', version '' started. IsReplay: False. Input: [\"Kah-BOOOOM!!!\"]",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})', version '' failed with an error. Reason: Microsoft.Azure.WebJobs.Host.FunctionInvocationException",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: True. Input: \"Kah-BOOOOM!!!\"",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})', version '' scheduled. Reason: Throw. IsReplay: True.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' failed with an error. Reason: Microsoft.Azure.WebJobs.FunctionFailedException",
            };

            return list;
        }

        private static List<string> GetLogs_UnhandledActivityExceptionWithRetry(string messageId, string[] orchestratorFunctionNames, string activityFunctionName)
        {
            var list = new List<string>()
            {
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' scheduled. Reason: NewInstance. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: False. Input: \"Kah-BOOOOM!!!\"",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})', version '' scheduled. Reason: ActivityThrowWithRetry. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' awaited. IsReplay: False.",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})', version '' started. IsReplay: False. Input: [\"Kah-BOOOOM!!!\"]",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})', version '' failed with an error. Reason: Microsoft.Azure.WebJobs.Host.FunctionInvocationException",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: True. Input: \"Kah-BOOOOM!!!\"",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})', version '' scheduled. Reason: ActivityThrowWithRetry. IsReplay: True.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' awaited. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: True. Input: \"Kah-BOOOOM!!!\"",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})', version '' scheduled. Reason: ActivityThrowWithRetry. IsReplay: True.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' awaited. IsReplay: False.",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})', version '' started. IsReplay: False. Input: [\"Kah-BOOOOM!!!\"]",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})', version '' failed with an error. Reason: Microsoft.Azure.WebJobs.Host.FunctionInvocationException",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: True. Input: \"Kah-BOOOOM!!!\"",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})', version '' scheduled. Reason: ActivityThrowWithRetry. IsReplay: True.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' awaited. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: True.",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})', version '' scheduled. Reason: ActivityThrowWithRetry. IsReplay: True.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' awaited. IsReplay: False.",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})', version '' started. IsReplay: False. Input: [\"Kah-BOOOOM!!!\"]",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})', version '' failed with an error. Reason: Microsoft.Azure.WebJobs.Host.FunctionInvocationException",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: True. Input: \"Kah-BOOOOM!!!\"",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})', version '' scheduled. Reason: ActivityThrowWithRetry. IsReplay: True.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' awaited. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: True. Input: \"Kah-BOOOOM!!!\"",
                $"{messageId}: Function '{activityFunctionName} ({FunctionType.Activity})', version '' scheduled. Reason: ActivityThrowWithRetry. IsReplay: True.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' failed with an error. Reason: Microsoft.Azure.WebJobs.FunctionFailedException",
            };

            return list;
        }

        private static List<string> GetLogs_Orchestration_OnUnregisteredActivity(string messageId, string[] orchestratorFunctionNames)
        {
            var list = new List<string>()
            {
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' scheduled. Reason: NewInstance. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' failed with an error. Reason: System.ArgumentException: The function 'UnregisteredActivity' doesn't exist, is disabled, or is not an activity function.",
            };

            return list;
        }

        private static List<string> GetLogs_Orchestration_OnUnregisteredOrchestrator(string messageId, string[] orchestratorFunctionNames)
        {
            var list = new List<string>()
            {
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' scheduled. Reason: NewInstance. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' failed with an error. Reason: System.ArgumentException: The function 'UnregisteredOrchestrator' doesn't exist, is disabled, or is not an orchestrator function.",
            };

            return list;
        }

        private static List<string> GetLogs_Orchestration_OnValidOrchestrator(string[] messageIds, string[] orchestratorFunctionNames, string activityFunctionName)
        {
            var list = new List<string>()
            {
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' scheduled. Reason: NewInstance. IsReplay: False.",
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: False.",
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[1]} ({FunctionType.Orchestrator})', version '' scheduled. Reason: CallOrchestrator. IsReplay: False.",
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' awaited. IsReplay: False.",
                $"{messageIds[1]}:0: Function '{orchestratorFunctionNames[1]} ({FunctionType.Orchestrator})', version '' started. IsReplay: False.",
                $"{messageIds[1]}:0: Function '{activityFunctionName} ({FunctionType.Activity})', version '' scheduled. Reason: SayHelloWithActivity. IsReplay: False.",
                $"{messageIds[1]}:0: Function '{orchestratorFunctionNames[1]} ({FunctionType.Orchestrator})', version '' awaited. IsReplay: False.",
                $"{messageIds[1]}:0: Function '{activityFunctionName} ({FunctionType.Activity})', version '' started. IsReplay: False.",
                $"{messageIds[1]}:0: Function '{activityFunctionName} ({FunctionType.Activity})', version '' completed. ContinuedAsNew: False. IsReplay: False. Output: \"Hello, ",
                $"{messageIds[1]}:0: Function '{orchestratorFunctionNames[1]} ({FunctionType.Orchestrator})', version '' started. IsReplay: True.",
                $"{messageIds[1]}:0: Function '{activityFunctionName} ({FunctionType.Activity})', version '' scheduled. Reason: SayHelloWithActivity. IsReplay: True.",
                $"{messageIds[1]}:0: Function '{orchestratorFunctionNames[1]} ({FunctionType.Orchestrator})', version '' completed. ContinuedAsNew: False. IsReplay: False. Output: \"Hello,",
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: True.",
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[1]} ({FunctionType.Orchestrator})', version '' scheduled. Reason: CallOrchestrator. IsReplay: True.",
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' completed. ContinuedAsNew: False. IsReplay: False. Output: \"Hello,",
            };

            return list;
        }

        private static List<string> GetLogs_Orchestration_Activity(string[] messageIds, string[] orchestratorFunctionNames, string activityFunctionName)
        {
            var list = new List<string>()
            {
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' scheduled. Reason: NewInstance. IsReplay: False.",
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: False.",
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[1]} ({FunctionType.Orchestrator})', version '' scheduled. Reason: OrchestratorGreeting. IsReplay: False.",
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' awaited. IsReplay: False.",
                $"{messageIds[1]}:0: Function '{orchestratorFunctionNames[1]} ({FunctionType.Orchestrator})', version '' started. IsReplay: False.",
                $"{messageIds[1]}:0: Function '{activityFunctionName} ({FunctionType.Activity})', version '' scheduled. Reason: SayHelloWithActivity. IsReplay: False.",
                $"{messageIds[1]}:0: Function '{orchestratorFunctionNames[1]} ({FunctionType.Orchestrator})', version '' awaited. IsReplay: False.",
                $"{messageIds[1]}:0: Function '{activityFunctionName} ({FunctionType.Activity})', version '' started. IsReplay: False.",
                $"{messageIds[1]}:0: Function '{activityFunctionName} ({FunctionType.Activity})', version '' completed. ContinuedAsNew: False. IsReplay: False. Output: \"Hello, ",
                $"{messageIds[1]}:0: Function '{orchestratorFunctionNames[1]} ({FunctionType.Orchestrator})', version '' started. IsReplay: True.",
                $"{messageIds[1]}:0: Function '{activityFunctionName} ({FunctionType.Activity})', version '' scheduled. Reason: SayHelloWithActivity. IsReplay: True.",
                $"{messageIds[1]}:0: Function '{orchestratorFunctionNames[1]} ({FunctionType.Orchestrator})', version '' completed. ContinuedAsNew: False. IsReplay: False. Output: \"Hello,",
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: True.",
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[1]} ({FunctionType.Orchestrator})', version '' scheduled. Reason: OrchestratorGreeting. IsReplay: True.",
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' completed. ContinuedAsNew: False. IsReplay: False. Output: (null)",
            };

            return list;
        }

        private static string GetMessageId(string message)
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

        private static string GetLatencyMs(string message)
        {
            const string LatencyMsPrefix = " Latency:";
            int start = message.IndexOf(LatencyMsPrefix) + LatencyMsPrefix.Length;
            int end = message.IndexOf('m', start) - 1;
            return message.Substring(start, end - start);
        }

        private class MockNameResolver : INameResolver
        {
            private string value;

            public MockNameResolver(string value)
            {
                this.value = value;
            }

            public string Resolve(string name)
            {
                return value;
            }
        }
    }
}
