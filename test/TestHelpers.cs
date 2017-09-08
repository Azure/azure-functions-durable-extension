// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal static class TestHelpers
    {
        public const string LogCategory = "Host.Triggers.DurableTask";

        public static JobHost GetJobHost(ILoggerFactory loggerFactory, string taskHub = "CommonTestHub")
        {
            var config = new JobHostConfiguration { HostId = "durable-task-host" };
            config.ConfigureDurableFunctionTypeLocator(typeof(TestOrchestrations), typeof(TestActivities));
            config.UseDurableTask(new DurableTaskExtension
            {
                HubName = taskHub.Replace("_", ""),
                TraceInputsAndOutputs = true
            });

            // Performance is *significantly* worse when dashboard logging is enabled, at least
            // when running in the storage emulator. Disabling to keep tests running quickly.
            config.DashboardConnectionString = null;

            // Add test logger
            config.LoggerFactory = loggerFactory;

            var host = new JobHost(config);
            return host;
        }

        public static void AssertLogMessageSequence(TestLoggerProvider loggerProvider, string testName,
            string[] orchestratorFunctionNames, string activityFunctionName = null)
        {
            var logger = loggerProvider.CreatedLoggers.Single(l => l.Category == LogCategory);
            var logMessages = logger.LogMessages.ToList();
            var messageIds = new List<string>()
            {
                GetMessageId(logMessages[0].FormattedMessage)
            };

            string timeStamp = string.Empty;
            
            if (testName.Equals("TimerCancellation", StringComparison.InvariantCultureIgnoreCase)
                || testName.Equals("TimerExpiration", StringComparison.InvariantCultureIgnoreCase))
            {
                timeStamp = GetTimerTimestamp(logMessages[3].FormattedMessage);
            }
            else if (testName.Equals("Orchestration_OnValidOrchestrator", StringComparison.InvariantCultureIgnoreCase))
            {
                messageIds.Add(GetMessageId(logMessages[4].FormattedMessage));
            }

            var expectedLogMessages = GetExpectedLogMessages(testName, messageIds, orchestratorFunctionNames, activityFunctionName, timeStamp);
            var actualLogMessage = logMessages.Select(m => m.FormattedMessage).ToList();

            Assert.True(
                logMessages.TrueForAll(m => m.Category.Equals(LogCategory, StringComparison.InvariantCultureIgnoreCase)));
            Assert.Equal(expectedLogMessages.Count, logMessages.Count);
            AssertLogMessages(expectedLogMessages, actualLogMessage);
        }

        private static IList<string> GetExpectedLogMessages(string testName, List<string> messageIds, string[] orchestratorFunctionNames, string activityFunctionName = null, string timeStamp = null)
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
                case "Orchestration_OnUnregisteredActivity":
                    messages = GetLogs_Orchestration_OnUnregisteredActivity(messageIds[0], orchestratorFunctionNames);
                    break;
                case "Orchestration_OnValidOrchestrator":
                    messages = GetLogs_Orchestration_OnValidOrchestrator(messageIds.ToArray(), orchestratorFunctionNames, activityFunctionName);
                    break;
                default:
                    break;
            }

            return messages;
        }

        private static void AssertLogMessages(IList<string> expected, IList<string> actual)
        {
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.StartsWith(expected[i], actual[i]);
            }
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
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' awaited. IsReplay: False."
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
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' completed. ContinuedAsNew: False. IsReplay: False. Output: \"Approved\""
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
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Activity})', version '' scheduled. Reason: Throw. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' awaited. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Activity})', version '' started. IsReplay: False. Input: [\"Kah-BOOOOM!!!\"]",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Activity})', version '' failed with an error. Reason: Microsoft.Azure.WebJobs.Host.FunctionInvocationException",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: True. Input: \"Kah-BOOOOM!!!\"",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Activity})', version '' scheduled. Reason: Throw. IsReplay: True.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' failed with an error. Reason: DurableTask.Core.Exceptions.TaskFailedException",
            };

            return list;
        }

        private static List<string> GetLogs_Orchestration_OnUnregisteredActivity(string messageId, string[] orchestratorFunctionNames)
        {
            var list = new List<string>()
            {
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' scheduled. Reason: NewInstance. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: False.",
                $"{messageId}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' failed with an error. Reason: System.ArgumentException: The function 'UnregisteredActivity' doesn't exist, is disabled, or is not an activity or orchestrator function.",
            };

            return list;
        }

        private static List<string> GetLogs_Orchestration_OnValidOrchestrator(string[] messageIds, string[] orchestratorFunctionNames, string activityFunctionName)
        {
            var list = new List<string>()
            {
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' scheduled. Reason: NewInstance. IsReplay: False.",
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' started. IsReplay: False.",
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[1]} ({FunctionType.Orchestrator})', version '' scheduled. Reason: CallActivity. IsReplay: False.",
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
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[1]} ({FunctionType.Orchestrator})', version '' scheduled. Reason: CallActivity. IsReplay: True.",
                $"{messageIds[0]}: Function '{orchestratorFunctionNames[0]} ({FunctionType.Orchestrator})', version '' completed. ContinuedAsNew: False. IsReplay: False. Output: \"Hello,",
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
    }
}
