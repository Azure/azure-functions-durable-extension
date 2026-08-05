// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.History;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class DurableClientHistoryTests
    {
        private const string InstanceId = "history-instance";
        private const string ExecutionId = "history-execution";
        private const string ActivityInput = "{\"value\":1}";
        private const string FailedActivityInput = "{\"value\":2}";
        private const string InFlightActivityInput = "{\"value\":3}";
        private const string ChildInput = "{\"child\":1}";
        private const string FailedChildInput = "{\"child\":2}";
        private const string InFlightChildInput = "{\"child\":3}";
        private const string CompletedChildInstanceId = "completed-child-instance";
        private const string FailedChildInstanceId = "failed-child-instance";
        private const string InFlightChildInstanceId = "in-flight-child-instance";
        private const string StringResult = "\"activity-result\"";
        private const string FailureResult = "\"failure-result\"";
        private const string ObjectResult = "{\"ok\":true}";
        private const string FinalResult = "{\"final\":true}";

        private static readonly DateTime StartTime = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        public static TheoryData<bool, bool> HistoryProjectionOptions => new TheoryData<bool, bool>
        {
            { false, false },
            { false, true },
            { true, false },
            { true, true },
        };

        public static TheoryData<int[]> HistoryRemovalCases => new TheoryData<int[]>
        {
            new[] { 0, 0, 0 },
            new[] { 1, 1, 1 },
            new[] { 1, 0, 1, 0, 1 },
            new[] { 0, 2, 0, 0, 0 },
            new[] { 2, 0 },
        };

        [Theory]
        [MemberData(nameof(HistoryProjectionOptions))]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task GetStatusAsync_ProjectsActivityHistoryWithoutChangingPublicShape(bool showInput, bool showHistoryOutput)
        {
            JArray actual = await GetProjectedHistoryAsync(CreateActivityHistory(), showInput, showHistoryOutput);

            AssertJsonEqual(CreateExpectedActivityHistory(showInput, showHistoryOutput), actual);
        }

        [Theory]
        [MemberData(nameof(HistoryProjectionOptions))]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task GetStatusAsync_ProjectsSubOrchestrationHistoryWithInstanceIds(bool showInput, bool showHistoryOutput)
        {
            JArray actual = await GetProjectedHistoryAsync(CreateSubOrchestrationHistory(), showInput, showHistoryOutput);

            AssertJsonEqual(CreateExpectedSubOrchestrationHistory(showInput, showHistoryOutput), actual);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task GetStatusAsync_PreservesUnknownEventsWhenNoEventsAreRemoved()
        {
            var textUnknownEvent = new JObject
            {
                ["EventType"] = "FutureEvent",
                ["EventId"] = 91,
                ["IsPlayed"] = true,
                ["Timestamp"] = StartTime,
                ["Input"] = "future-input",
                ["Payload"] = new JObject { ["future"] = true },
            };
            var numericUnknownEvent = CreateEvent(
                (EventType)999,
                92,
                StartTime.AddSeconds(1),
                new JProperty("Input", "numeric-input"),
                new JProperty("Payload", "numeric-payload"));
            var timerFired = CreateEvent(
                EventType.TimerFired,
                93,
                StartTime.AddSeconds(2),
                new JProperty("TimerId", 12),
                new JProperty("FireAt", StartTime.AddMinutes(1)),
                new JProperty("Input", "timer-input"));
            string history = new JArray(textUnknownEvent, numericUnknownEvent, timerFired).ToString(Formatting.None);

            JArray actual = await GetProjectedHistoryAsync(history, showInput: false, showHistoryOutput: false);

            var expected = new JArray(
                textUnknownEvent,
                new JObject
                {
                    ["EventType"] = "999",
                    ["Timestamp"] = StartTime.AddSeconds(1),
                    ["Payload"] = "numeric-payload",
                },
                new JObject
                {
                    ["EventType"] = "TimerFired",
                    ["Timestamp"] = StartTime.AddSeconds(2),
                    ["FireAt"] = StartTime.AddMinutes(1),
                });
            AssertJsonEqual(expected, actual);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task GetStatusAsync_RemovesHistoryContainingOnlyReplayMarkers()
        {
            string history = new JArray(
                CreateEvent(EventType.OrchestratorStarted, 1, StartTime),
                CreateEvent(EventType.OrchestratorCompleted, 2, StartTime.AddSeconds(1)),
                CreateEvent(EventType.OrchestratorStarted, 3, StartTime.AddSeconds(2)),
                CreateEvent(EventType.OrchestratorCompleted, 4, StartTime.AddSeconds(3))).ToString(Formatting.None);

            JArray actual = await GetProjectedHistoryAsync(history, showInput: true, showHistoryOutput: true);

            Assert.Empty(actual);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task GetStatusAsync_MalformedHistoryPreservesExceptionBehavior()
        {
            string history = new JArray(42).ToString(Formatting.None);

            await Assert.ThrowsAsync<InvalidCastException>(
                () => GetProjectedHistoryAsync(history, showInput: true, showHistoryOutput: true));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task GetStatusAsync_DuplicateCompletionsPreserveAdjustedRemovalBehavior()
        {
            var firstUnknownEvent = new JObject
            {
                ["EventType"] = "FutureEvent",
                ["Payload"] = "removed-by-existing-behavior",
            };
            var retainedUnknownEvent = new JObject
            {
                ["EventType"] = "FutureEvent",
                ["Payload"] = "retained",
            };
            string history = new JArray(
                firstUnknownEvent,
                CreateEvent(
                    EventType.TaskScheduled,
                    70,
                    StartTime,
                    new JProperty("Name", "DuplicatedCompletionActivity"),
                    new JProperty("Input", ActivityInput),
                    new JProperty("Version", "v1")),
                retainedUnknownEvent,
                CreateEvent(
                    EventType.TaskCompleted,
                    71,
                    StartTime.AddSeconds(1),
                    new JProperty("TaskScheduledId", 70),
                    new JProperty("Result", StringResult)),
                CreateEvent(
                    EventType.TaskCompleted,
                    72,
                    StartTime.AddSeconds(2),
                    new JProperty("TaskScheduledId", 70),
                    new JProperty("Result", StringResult))).ToString(Formatting.None);

            JArray actual = await GetProjectedHistoryAsync(history, showInput: true, showHistoryOutput: true);

            var expectedCompletion1 = new JObject
            {
                ["EventType"] = "TaskCompleted",
                ["Timestamp"] = StartTime.AddSeconds(1),
                ["Result"] = "activity-result",
                ["ScheduledTime"] = StartTime.ToLocalTime(),
                ["FunctionName"] = "DuplicatedCompletionActivity",
                ["Input"] = ActivityInput,
            };
            var expectedCompletion2 = new JObject
            {
                ["EventType"] = "TaskCompleted",
                ["Timestamp"] = StartTime.AddSeconds(2),
                ["Result"] = "activity-result",
                ["ScheduledTime"] = StartTime.ToLocalTime(),
                ["FunctionName"] = "DuplicatedCompletionActivity",
                ["Input"] = ActivityInput,
            };
            AssertJsonEqual(
                new JArray(retainedUnknownEvent, expectedCompletion1, expectedCompletion2),
                actual);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CompactHistory_ReparentsRetainedTokensWithoutCloning()
        {
            var first = new JObject { ["EventType"] = "First" };
            var removed = new JObject { ["EventType"] = "Removed" };
            var last = new JObject { ["EventType"] = "Last" };
            var history = new JArray(first, removed, last);

            DurableClient.CompactHistory(history, new[] { 0, 1, 0 });

            Assert.Same(first, history[0]);
            Assert.Same(last, history[1]);
            Assert.Same(history, first.Parent);
            Assert.Same(history, last.Parent);
            Assert.Null(removed.Parent);
        }

        [Theory]
        [MemberData(nameof(HistoryRemovalCases))]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CompactHistory_MatchesLegacyAdjustedRemovalSemantics(int[] eventsToRemove)
        {
            JArray expected = CreateIndexedHistory(eventsToRemove.Length);
            JArray actual = CreateIndexedHistory(eventsToRemove.Length);

            Exception expectedException = Record.Exception(() => LegacyCompactHistory(expected, eventsToRemove));
            Exception actualException = Record.Exception(() => DurableClient.CompactHistory(actual, eventsToRemove));

            Assert.Equal(expectedException?.GetType(), actualException?.GetType());
            Assert.Equal(expectedException?.Message, actualException?.Message);
            if (expectedException == null)
            {
                AssertJsonEqual(expected, actual);
            }
        }

        private static string CreateActivityHistory()
        {
            return new JArray(
                CreateEvent(EventType.OrchestratorStarted, 0, StartTime),
                CreateEvent(
                    EventType.ExecutionStarted,
                    1,
                    StartTime.AddSeconds(1),
                    new JProperty("Name", "RootOrchestration"),
                    new JProperty("Input", "{\"root\":true}"),
                    new JProperty("OrchestrationInstance", new JObject { ["InstanceId"] = InstanceId }),
                    new JProperty("ParentInstance", null),
                    new JProperty("Version", "v1"),
                    new JProperty("Tags", new JObject { ["tag"] = "value" }),
                    new JProperty("Correlation", "root-correlation")),
                CreateEvent(
                    EventType.TaskScheduled,
                    10,
                    StartTime.AddSeconds(2),
                    new JProperty("Name", "RepeatedActivity"),
                    new JProperty("Input", ActivityInput),
                    new JProperty("Version", "v1")),
                CreateEvent(EventType.OrchestratorCompleted, 2, StartTime.AddSeconds(3)),
                CreateEvent(EventType.OrchestratorStarted, 3, StartTime.AddSeconds(4)),
                CreateEvent(
                    EventType.TaskCompleted,
                    11,
                    StartTime.AddSeconds(5),
                    new JProperty("TaskScheduledId", 10),
                    new JProperty("Result", StringResult),
                    new JProperty("Extension", "completed-extension")),
                CreateEvent(
                    EventType.TaskScheduled,
                    20,
                    StartTime.AddSeconds(6),
                    new JProperty("Name", "RepeatedActivity"),
                    new JProperty("Input", FailedActivityInput),
                    new JProperty("Version", "v2")),
                CreateEvent(
                    EventType.TaskFailed,
                    21,
                    StartTime.AddSeconds(7),
                    new JProperty("TaskScheduledId", 20),
                    new JProperty("Reason", "activity failed"),
                    new JProperty("Details", "activity details"),
                    new JProperty("Result", FailureResult),
                    new JProperty("Extension", "failed-extension")),
                CreateEvent(
                    EventType.TaskScheduled,
                    30,
                    StartTime.AddSeconds(8),
                    new JProperty("Name", "InFlightActivity"),
                    new JProperty("Input", InFlightActivityInput),
                    new JProperty("Version", "v3"),
                    new JProperty("Extension", "in-flight-extension")),
                CreateEvent(
                    EventType.ExecutionCompleted,
                    31,
                    StartTime.AddSeconds(9),
                    new JProperty("OrchestrationStatus", (int)OrchestrationStatus.Completed),
                    new JProperty("Result", FinalResult),
                    new JProperty("Extension", "execution-extension")),
                CreateEvent(EventType.OrchestratorCompleted, 4, StartTime.AddSeconds(10))).ToString(Formatting.None);
        }

        private static JArray CreateExpectedActivityHistory(bool showInput, bool showHistoryOutput)
        {
            var executionStarted = new JObject
            {
                ["EventType"] = "ExecutionStarted",
                ["Timestamp"] = StartTime.AddSeconds(1),
            };
            if (showInput)
            {
                executionStarted["Input"] = "{\"root\":true}";
            }

            executionStarted["Correlation"] = "root-correlation";
            executionStarted["FunctionName"] = "RootOrchestration";

            var taskCompleted = new JObject
            {
                ["EventType"] = "TaskCompleted",
                ["Timestamp"] = StartTime.AddSeconds(5),
            };
            if (showHistoryOutput)
            {
                taskCompleted["Result"] = "activity-result";
            }

            taskCompleted["Extension"] = "completed-extension";
            taskCompleted["ScheduledTime"] = StartTime.AddSeconds(2).ToLocalTime();
            taskCompleted["FunctionName"] = "RepeatedActivity";
            if (showInput)
            {
                taskCompleted["Input"] = ActivityInput;
            }

            var taskFailed = new JObject
            {
                ["EventType"] = "TaskFailed",
                ["Timestamp"] = StartTime.AddSeconds(7),
                ["Reason"] = "activity failed",
                ["Details"] = "activity details",
                ["Result"] = FailureResult,
                ["Extension"] = "failed-extension",
                ["ScheduledTime"] = StartTime.AddSeconds(6).ToLocalTime(),
                ["FunctionName"] = "RepeatedActivity",
            };
            if (showInput)
            {
                taskFailed["Input"] = FailedActivityInput;
            }

            var taskScheduled = new JObject
            {
                ["EventType"] = "TaskScheduled",
                ["Timestamp"] = StartTime.AddSeconds(8),
                ["Name"] = "InFlightActivity",
            };
            if (showInput)
            {
                taskScheduled["Input"] = InFlightActivityInput;
            }

            taskScheduled["Extension"] = "in-flight-extension";

            var executionCompleted = new JObject
            {
                ["EventType"] = "ExecutionCompleted",
                ["Timestamp"] = StartTime.AddSeconds(9),
                ["OrchestrationStatus"] = "Completed",
            };
            if (showHistoryOutput)
            {
                executionCompleted["Result"] = new JObject { ["final"] = true };
            }

            executionCompleted["Extension"] = "execution-extension";

            return new JArray(executionStarted, taskCompleted, taskFailed, taskScheduled, executionCompleted);
        }

        private static string CreateSubOrchestrationHistory()
        {
            return new JArray(
                CreateEvent(
                    EventType.SubOrchestrationInstanceCreated,
                    40,
                    StartTime,
                    new JProperty("Name", "RepeatedChild"),
                    new JProperty("InstanceId", CompletedChildInstanceId),
                    new JProperty("Input", ChildInput),
                    new JProperty("Version", "v1")),
                CreateEvent(
                    EventType.SubOrchestrationInstanceCompleted,
                    41,
                    StartTime.AddSeconds(1),
                    new JProperty("TaskScheduledId", 40),
                    new JProperty("Result", ObjectResult),
                    new JProperty("Extension", "completed-child-extension")),
                CreateEvent(
                    EventType.SubOrchestrationInstanceCreated,
                    50,
                    StartTime.AddSeconds(2),
                    new JProperty("Name", "RepeatedChild"),
                    new JProperty("InstanceId", FailedChildInstanceId),
                    new JProperty("Input", FailedChildInput),
                    new JProperty("Version", "v2")),
                CreateEvent(
                    EventType.SubOrchestrationInstanceFailed,
                    51,
                    StartTime.AddSeconds(3),
                    new JProperty("TaskScheduledId", 50),
                    new JProperty("Reason", "child failed"),
                    new JProperty("Details", "child details"),
                    new JProperty("Result", FailureResult),
                    new JProperty("Extension", "failed-child-extension")),
                CreateEvent(
                    EventType.SubOrchestrationInstanceCreated,
                    60,
                    StartTime.AddSeconds(4),
                    new JProperty("Name", "InFlightChild"),
                    new JProperty("InstanceId", InFlightChildInstanceId),
                    new JProperty("Input", InFlightChildInput),
                    new JProperty("Version", "v3"),
                    new JProperty("Extension", "in-flight-child-extension"))).ToString(Formatting.None);
        }

        private static JArray CreateExpectedSubOrchestrationHistory(bool showInput, bool showHistoryOutput)
        {
            var completed = new JObject
            {
                ["EventType"] = "SubOrchestrationInstanceCompleted",
                ["Timestamp"] = StartTime.AddSeconds(1),
            };
            if (showHistoryOutput)
            {
                completed["Result"] = new JObject { ["ok"] = true };
            }

            completed["Extension"] = "completed-child-extension";
            completed["ScheduledTime"] = StartTime.ToLocalTime();
            completed["FunctionName"] = "RepeatedChild";
            completed["InstanceId"] = CompletedChildInstanceId;
            if (showInput)
            {
                completed["Input"] = ChildInput;
            }

            var failed = new JObject
            {
                ["EventType"] = "SubOrchestrationInstanceFailed",
                ["Timestamp"] = StartTime.AddSeconds(3),
                ["Reason"] = "child failed",
                ["Details"] = "child details",
                ["Result"] = FailureResult,
                ["Extension"] = "failed-child-extension",
                ["ScheduledTime"] = StartTime.AddSeconds(2).ToLocalTime(),
                ["FunctionName"] = "RepeatedChild",
                ["InstanceId"] = FailedChildInstanceId,
            };
            if (showInput)
            {
                failed["Input"] = FailedChildInput;
            }

            var inFlight = new JObject
            {
                ["EventType"] = "SubOrchestrationInstanceCreated",
                ["Timestamp"] = StartTime.AddSeconds(4),
                ["Name"] = "InFlightChild",
                ["InstanceId"] = InFlightChildInstanceId,
            };
            if (showInput)
            {
                inFlight["Input"] = InFlightChildInput;
            }

            inFlight["Extension"] = "in-flight-child-extension";

            return new JArray(completed, failed, inFlight);
        }

        private static JObject CreateEvent(EventType eventType, int eventId, DateTime timestamp, params JProperty[] properties)
        {
            var historyEvent = new JObject
            {
                ["EventType"] = (int)eventType,
                ["EventId"] = eventId,
                ["IsPlayed"] = false,
                ["Timestamp"] = timestamp,
            };

            foreach (JProperty property in properties)
            {
                historyEvent.Add(property);
            }

            return historyEvent;
        }

        private static JArray CreateIndexedHistory(int count)
        {
            var history = new JArray();
            for (int i = 0; i < count; i++)
            {
                history.Add(i);
            }

            return history;
        }

        private static void LegacyCompactHistory(JArray history, int[] eventsToRemove)
        {
            int removalsApplied = 0;
            for (int sourceIndex = 0; sourceIndex < eventsToRemove.Length; sourceIndex++)
            {
                for (int duplicate = 0; duplicate < eventsToRemove[sourceIndex]; duplicate++)
                {
                    history.RemoveAt(sourceIndex - removalsApplied);
                    removalsApplied++;
                }
            }
        }

        private static async Task<JArray> GetProjectedHistoryAsync(string history, bool showInput, bool showHistoryOutput)
        {
            var serviceClient = new Mock<IOrchestrationServiceClient>(MockBehavior.Strict);
            serviceClient
                .Setup(client => client.GetOrchestrationStateAsync(InstanceId, false))
                .ReturnsAsync(new List<OrchestrationState>
                {
                    new OrchestrationState
                    {
                        Name = "RootOrchestration",
                        OrchestrationInstance = new OrchestrationInstance
                        {
                            InstanceId = InstanceId,
                            ExecutionId = ExecutionId,
                        },
                        OrchestrationStatus = OrchestrationStatus.Completed,
                        CreatedTime = StartTime,
                        LastUpdatedTime = StartTime.AddMinutes(1),
                    },
                });
            serviceClient
                .Setup(client => client.GetOrchestrationHistoryAsync(InstanceId, ExecutionId))
                .ReturnsAsync(history);

            var durabilityProvider = new DurabilityProvider(
                "test",
                new Mock<IOrchestrationService>().Object,
                serviceClient.Object,
                "test");
            var options = new DurableTaskOptions { HubName = "HistoryTestHub" };
            var messageDataConverter = new MessagePayloadDataConverter(new JsonSerializerSettings(), true);
            var traceHelper = new EndToEndTraceHelper(
                new NullLogger<EndToEndTraceHelper>(),
                options.Tracing.TraceReplayEvents);
            var durableClient = (IDurableOrchestrationClient)new DurableClient(
                durabilityProvider,
                httpHandler: null,
                new DurableClientAttribute { TaskHub = options.HubName },
                messageDataConverter,
                traceHelper,
                options);

            DurableOrchestrationStatus status = await durableClient.GetStatusAsync(
                InstanceId,
                showHistory: true,
                showHistoryOutput,
                showInput);
            return status.History;
        }

        private static void AssertJsonEqual(JArray expected, JArray actual)
        {
            Assert.Equal(expected.ToString(Formatting.None), actual.ToString(Formatting.None));
        }
    }
}
