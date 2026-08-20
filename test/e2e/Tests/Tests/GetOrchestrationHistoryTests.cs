// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using DurableTask.Core;
using DurableTask.Core.History;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.DurableTask.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class GetOrchestrationHistoryTests
{
    private readonly FunctionAppFixture fixture;
    private readonly ITestOutputHelper output;
    // Make the results of the activity/entity calls around 1 MB so the orchestration history exceeds the max limit of the history chunk size (2 MB)
    // We make it slightly smaller than 1 MB to avoid exceeding the current payload size limit in DTS, but just large enough to force chunking
    private const int OutputSize = 1024 * 1024 - 5;
    private const string TagsKey = "key";
    private const string TagsValue = "value";

    private readonly Dictionary<string, string> tags = new() { { TagsKey, TagsValue } };

    // Duplicated from GetOrchestrationHistory since the import is failing - see comment above the ComplexInput class.
    private static readonly EntityInstanceId entityId = new("SimpleEntity", "singleton");

    public GetOrchestrationHistoryTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(testOutputHelper);
        this.output = testOutputHelper;
    }

    [Fact]
    [Trait("Java", "Skip")] // GetOrchestrationHistory_HttpStart is only defined in the .NET isolated test app
    [Trait("Python", "Skip")] // GetOrchestrationHistory_HttpStart is only defined in the .NET isolated test app
    [Trait("PowerShell", "Skip")] // GetOrchestrationHistory_HttpStart is only defined in the .NET isolated test app
    [Trait("Node", "Skip")] // GetOrchestrationHistory_HttpStart is only defined in the .NET isolated test app
    [Trait("MSSQL", "Skip")] // MSSQL does not include InstanceId in SubOrchestrationInstanceCreated events
    public async Task GetStatusHistory_CompletedSubOrchestrationIncludesInstanceId()
    {
        string subOrchestrationInstanceId = Guid.NewGuid().ToString();

        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(
            "GetOrchestrationHistory_HttpStart",
            $"?orchestrationType=succeed&subOrchestrationInstanceId={subOrchestrationInstanceId}&outputSize=16&callEntities=false&tagsKey={TagsKey}&tagsValue={TagsValue}");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        string separator = statusQueryGetUri.Contains('?') ? "&" : "?";
        using var httpClient = new HttpClient();
        string statusJson = await httpClient.GetStringAsync($"{statusQueryGetUri}{separator}showHistory=true");
        JObject status = JObject.Parse(statusJson);
        JArray historyEvents = Assert.IsType<JArray>(status["historyEvents"]);
        JObject completedEvent = Assert.Single(
            historyEvents.OfType<JObject>(),
            historyEvent => (string?)historyEvent["EventType"] == nameof(EventType.SubOrchestrationInstanceCompleted));

        Assert.Equal(subOrchestrationInstanceId, (string?)completedEvent["InstanceId"]);
    }

    [Fact]
    [Trait("Java", "Skip")] // The GetOrchestrationHistory API is not implemented in Java
    [Trait("Python", "Skip")] // The GetOrchestrationHistory API is not implemented in Python
    [Trait("PowerShell", "Skip")] // The GetOrchestrationHistory API is not implemented in PowerShell
    [Trait("Node", "Skip")] // The GetOrchestrationHistory API is not implemented in Node
    /// Tests that the nested failure details of an orchestration and its failed suborchestration can be retrieved successfully
    public async Task GetOrchestrationHistory_FailedOrchestration()
    {
        bool isNotMSSQL = this.fixture.GetDurabilityProvider() != FunctionAppFixture.ConfiguredDurabilityProviderType.MSSQL;
        string subOrchestrationInstanceId = Guid.NewGuid().ToString();

        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(
            "GetOrchestrationHistory_HttpStart",
            $"?orchestrationType=fail&subOrchestrationInstanceId={subOrchestrationInstanceId}&outputSize={OutputSize}&callEntities={isNotMSSQL.ToString().ToLower()}&tagsKey={TagsKey}&tagsValue={TagsValue}");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Failed", 30);

        using HttpResponseMessage getOrchestrationHistoryResponse = await HttpHelpers.InvokeHttpTrigger("GetInstanceHistory", $"?instanceId={instanceId}");
        Assert.Equal(HttpStatusCode.OK, getOrchestrationHistoryResponse.StatusCode);
        string jsonHistory = await getOrchestrationHistoryResponse.Content.ReadAsStringAsync();
        List<HistoryEvent>? historyEvents = JsonConvert.DeserializeObject<List<HistoryEvent>>(
            jsonHistory,
            new JsonSerializerSettings()
            {
                Converters = { new HistoryEventJsonConverter() },
            });
        Assert.NotNull(historyEvents);

        // Confirm the correct count and sequence of events
        Assert.Equal(8, historyEvents.Count);

        // OrchestratorStarted, ExecutionStarted, SubOrchestrationInstanceCreated, OrchestratorCompleted
        Assert.Equal(EventType.OrchestratorStarted, historyEvents[0].EventType);
        Assert.Equal(EventType.ExecutionStarted, historyEvents[1].EventType);
        // Confirm the fields of the ExecutionStartedEvent (name, orchestration input, and orchestration tags)
        var parentExecutionStartedEvent = (ExecutionStartedEvent)historyEvents[1];
        Assert.Equal("ParentOrchestration", parentExecutionStartedEvent.Name);
        Assert.Equal(new ComplexInput("fail", subOrchestrationInstanceId, OutputSize, isNotMSSQL, this.tags),
            JsonConvert.DeserializeObject<ComplexInput>(parentExecutionStartedEvent.Input));
        // MSSQL does not include tags in history events
        if (isNotMSSQL)
        {
            Assert.NotNull(parentExecutionStartedEvent.Tags);
            Assert.Contains(TagsKey, parentExecutionStartedEvent.Tags.Keys);
            Assert.Contains(TagsValue, parentExecutionStartedEvent.Tags.Values);
        }
        Assert.Equal(EventType.SubOrchestrationInstanceCreated, historyEvents[2].EventType);
        var subOrchestrationInstanceCreatedEvent = (SubOrchestrationInstanceCreatedEvent)historyEvents[2];
        Assert.Equal("FailSubOrchestration", subOrchestrationInstanceCreatedEvent.Name);
        // MSSQL does not include the instance ID field in the SubOrchestrationInstanceCreatedEvent
        if (isNotMSSQL)
        {
            Assert.Equal(subOrchestrationInstanceId, subOrchestrationInstanceCreatedEvent.InstanceId);
        }
        Assert.Equal(EventType.OrchestratorCompleted, historyEvents[3].EventType);

        // OrchestratorStarted, SubOrchestrationInstanceFailed, ExecutionCompleted, OrchestratorCompleted
        Assert.Equal(EventType.OrchestratorStarted, historyEvents[4].EventType);
        Assert.Equal(EventType.SubOrchestrationInstanceFailed, historyEvents[5].EventType);
        Assert.Equal(subOrchestrationInstanceCreatedEvent.EventId, ((SubOrchestrationInstanceFailedEvent)historyEvents[5]).TaskScheduledId);
        Assert.Equal(EventType.ExecutionCompleted, historyEvents[6].EventType);
        Assert.Equal(EventType.OrchestratorCompleted, historyEvents[7].EventType);

        // Now confirm the failure details field of the SubOrchestrationInstanceFailed and ExecutionCompleted events
        FailureDetails? parentFailureDetails = ((ExecutionCompletedEvent)historyEvents[6]).FailureDetails;
        FailureDetails? subOrchestrationFailureDetails = ((SubOrchestrationInstanceFailedEvent)historyEvents[5]).FailureDetails;

        Assert.NotNull(subOrchestrationFailureDetails);
        Assert.Equal("Microsoft.DurableTask.TaskFailedException", subOrchestrationFailureDetails.ErrorType);
        Assert.NotNull(subOrchestrationFailureDetails.InnerFailure);
        // The inner failure for the suborchestration failed event will be the actual exception thrown by the Activity, whereas the inner failure of the
        // execution completed event will be the suborchestration task failing
        Assert.Equal("System.Exception", subOrchestrationFailureDetails.InnerFailure.ErrorType);
        Assert.Equal("Failure!", subOrchestrationFailureDetails.InnerFailure.ErrorMessage);

        Assert.NotNull(parentFailureDetails);
        Assert.Equal("Microsoft.DurableTask.TaskFailedException", parentFailureDetails.ErrorType);
        Assert.NotNull(parentFailureDetails.InnerFailure);
        Assert.Equal("Microsoft.DurableTask.TaskFailedException", parentFailureDetails.InnerFailure.ErrorType);
        Assert.Equal(subOrchestrationFailureDetails.ErrorMessage, parentFailureDetails.InnerFailure.ErrorMessage);
        // Finally, the doubly nested inner failure of the execution completed event will correspond to the Activity failing
        Assert.NotNull(parentFailureDetails.InnerFailure.InnerFailure);
        Assert.Equal("Failure!", parentFailureDetails.InnerFailure.InnerFailure.ErrorMessage);

        using HttpResponseMessage getSubOrchestrationHistoryResponse = await HttpHelpers.InvokeHttpTrigger("GetInstanceHistory", $"?instanceId={subOrchestrationInstanceId}");
        Assert.Equal(HttpStatusCode.OK, getSubOrchestrationHistoryResponse.StatusCode);
        string subOrchestrationJsonHistory = await getSubOrchestrationHistoryResponse.Content.ReadAsStringAsync();
        List<HistoryEvent>? subOrchestrationHistoryEvents = JsonConvert.DeserializeObject<List<HistoryEvent>>(
            subOrchestrationJsonHistory,
            new JsonSerializerSettings()
            {
                Converters = { new HistoryEventJsonConverter() },
            });
        Assert.NotNull(subOrchestrationHistoryEvents);

        // Confirm the correct count and sequence of events for the suborchestration
        Assert.Equal(8, subOrchestrationHistoryEvents.Count);

        // OrchestratorStarted, ExecutionStarted, TaskScheduled, OrchestratorCompleted
        Assert.Equal(EventType.OrchestratorStarted, subOrchestrationHistoryEvents[0].EventType);
        Assert.Equal(EventType.ExecutionStarted, subOrchestrationHistoryEvents[1].EventType);
        // Confirm the fields of the ExecutionStartedEvent for the suborchestration (name, orchestration input, parent information, task ID)
        var subOrchestrationExecutionStartedEvent = (ExecutionStartedEvent)subOrchestrationHistoryEvents[1];
        Assert.Equal("FailSubOrchestration", subOrchestrationExecutionStartedEvent.Name);
        Assert.Equal(new ComplexInput(null, subOrchestrationInstanceId, OutputSize, isNotMSSQL, this.tags),
            JsonConvert.DeserializeObject<ComplexInput>(subOrchestrationExecutionStartedEvent.Input));
        Assert.Equal(parentExecutionStartedEvent.OrchestrationInstance.InstanceId, subOrchestrationExecutionStartedEvent.ParentInstance.OrchestrationInstance.InstanceId);
        Assert.Equal(subOrchestrationInstanceCreatedEvent.EventId, subOrchestrationExecutionStartedEvent.ParentInstance.TaskScheduleId);
        // MSSQL currently only adds the instance ID and task scheduled ID fields to the parent instance object
        if (isNotMSSQL)
        {
            Assert.Equal("ParentOrchestration", subOrchestrationExecutionStartedEvent.ParentInstance.Name);
            Assert.Equal(parentExecutionStartedEvent.OrchestrationInstance.ExecutionId, subOrchestrationExecutionStartedEvent.ParentInstance.OrchestrationInstance.ExecutionId);
        }
        // MSSQL does not include tags in history events
        if (isNotMSSQL)
        {
            Assert.NotNull(subOrchestrationExecutionStartedEvent.Tags);
            Assert.Contains(TagsKey, subOrchestrationExecutionStartedEvent.Tags.Keys);
            Assert.Contains(TagsValue, subOrchestrationExecutionStartedEvent.Tags.Values);
        }
        Assert.Equal(EventType.TaskScheduled, subOrchestrationHistoryEvents[2].EventType);
        var taskScheduledEvent = (TaskScheduledEvent)subOrchestrationHistoryEvents[2];
        Assert.Equal("ThrowExceptionActivity", taskScheduledEvent.Name);
        // MSSQL does not include tags in history events
        if (isNotMSSQL)
        {
            Assert.NotNull(taskScheduledEvent.Tags);
            Assert.Contains(TagsKey, taskScheduledEvent.Tags.Keys);
            Assert.Contains(TagsValue, taskScheduledEvent.Tags.Values);
        }
        Assert.Equal(EventType.OrchestratorCompleted, subOrchestrationHistoryEvents[3].EventType);

        // OrchestratorStarted, TaskFailed, ExecutionCompleted, OrchestratorCompleted
        Assert.Equal(EventType.OrchestratorStarted, subOrchestrationHistoryEvents[4].EventType);
        Assert.Equal(EventType.TaskFailed, subOrchestrationHistoryEvents[5].EventType);
        // Confirm the event ID of the TaskScheduledEvent matches the TaskScheduledId field of the TaskFailedEvent
        Assert.Equal(taskScheduledEvent.EventId, ((TaskFailedEvent)subOrchestrationHistoryEvents[5]).TaskScheduledId);
        Assert.Equal(EventType.ExecutionCompleted, subOrchestrationHistoryEvents[6].EventType);
        Assert.Equal(EventType.OrchestratorCompleted, subOrchestrationHistoryEvents[7].EventType);

        // Now confirm the failure details field of the TaskFailed and ExecutionCompleted events
        subOrchestrationFailureDetails = ((ExecutionCompletedEvent)subOrchestrationHistoryEvents[6]).FailureDetails;
        FailureDetails? taskFailureDetails = ((TaskFailedEvent)subOrchestrationHistoryEvents[5]).FailureDetails;

        Assert.NotNull(taskFailureDetails);
        Assert.Equal("System.Exception", taskFailureDetails.ErrorType);
        Assert.Equal("Failure!", taskFailureDetails.ErrorMessage);

        Assert.NotNull(subOrchestrationFailureDetails);
        Assert.Equal("Microsoft.DurableTask.TaskFailedException", subOrchestrationFailureDetails.ErrorType);
        Assert.NotNull(subOrchestrationFailureDetails.InnerFailure);
        // The inner failure for the suborchestration failed event will be the actual exception thrown by the Activity
        Assert.Equal(taskFailureDetails.ErrorType, subOrchestrationFailureDetails.InnerFailure.ErrorType);
        Assert.Equal(taskFailureDetails.ErrorMessage, subOrchestrationFailureDetails.InnerFailure.ErrorMessage);

        // Verify that the ClientOperationReceived logs were emitted with a FunctionInvocationId
        ClientOperationLogHelpers.AssertClientOperationLogExists(
            () => this.fixture.TestLogs.CoreToolsLogs,
            "StartOrchestration",
            instanceId,
            this.fixture.functionLanguageLocalizer.GetLanguageType());
        ClientOperationLogHelpers.AssertClientOperationLogExists(
            () => this.fixture.TestLogs.CoreToolsLogs,
            "StreamInstanceHistory",
            instanceId,
            this.fixture.functionLanguageLocalizer.GetLanguageType());
    }

    [Fact]
    [Trait("Java", "Skip")] // The GetOrchestrationHistory API is not implemented in Java
    [Trait("Python", "Skip")] // The GetOrchestrationHistory API is not implemented in Python
    [Trait("PowerShell", "Skip")] // The GetOrchestrationHistory API is not implemented in PowerShell
    [Trait("Node", "Skip")] // The GetOrchestrationHistory API is not implemented in Node
    /// Tests that an orchestration with a large history that exceeds the maximum size of a single history chunk (2 MB) and requires multiple chunks
    /// to be streamed can be retrieved successfully
    public async Task GetOrchestrationHistory_LargeHistory()
    {
        bool isNotMSSQL = this.fixture.GetDurabilityProvider() != FunctionAppFixture.ConfiguredDurabilityProviderType.MSSQL;
        string subOrchestrationInstanceId = Guid.NewGuid().ToString();

        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(
            "GetOrchestrationHistory_HttpStart",
            $"?orchestrationType=succeed&subOrchestrationInstanceId={subOrchestrationInstanceId}&outputSize={OutputSize}&callEntities={isNotMSSQL.ToString().ToLower()}&tagsKey={TagsKey}&tagsValue={TagsValue}");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        using HttpResponseMessage getOrchestrationHistoryResponse = await HttpHelpers.InvokeHttpTrigger("GetInstanceHistory", $"?instanceId={instanceId}");
        Assert.Equal(HttpStatusCode.OK, getOrchestrationHistoryResponse.StatusCode);
        string jsonHistory = await getOrchestrationHistoryResponse.Content.ReadAsStringAsync();
        List<HistoryEvent>? historyEvents = JsonConvert.DeserializeObject<List<HistoryEvent>>(
            jsonHistory,
            new JsonSerializerSettings()
            {
                Converters = { new HistoryEventJsonConverter() },
            });
        Assert.NotNull(historyEvents);

        // Confirm the correct count and sequence of events
        Assert.Equal(8, historyEvents.Count);

        // OrchestratorStarted, ExecutionStarted, SubOrchestrationInstanceCreated, OrchestratorCompleted
        Assert.Equal(EventType.OrchestratorStarted, historyEvents[0].EventType);
        Assert.Equal(EventType.ExecutionStarted, historyEvents[1].EventType);
        // Confirm the fields of the ExecutionStartedEvent (name, orchestration input, and orchestration tags)
        var parentExecutionStartedEvent = (ExecutionStartedEvent)historyEvents[1];
        Assert.Equal("ParentOrchestration", parentExecutionStartedEvent.Name);
        Assert.Equal(new ComplexInput("succeed", subOrchestrationInstanceId, OutputSize, isNotMSSQL, this.tags),
            JsonConvert.DeserializeObject<ComplexInput>(parentExecutionStartedEvent.Input));
        // MSSQL does not include tags in history events
        if (isNotMSSQL)
        {
            Assert.NotNull(parentExecutionStartedEvent.Tags);
            Assert.Contains(TagsKey, parentExecutionStartedEvent.Tags.Keys);
            Assert.Contains(TagsValue, parentExecutionStartedEvent.Tags.Values);
        }
        Assert.Equal(EventType.SubOrchestrationInstanceCreated, historyEvents[2].EventType);
        var subOrchestrationInstanceCreatedEvent = (SubOrchestrationInstanceCreatedEvent)historyEvents[2];
        Assert.Equal("CallLargeOutputTasksSubOrchestration", subOrchestrationInstanceCreatedEvent.Name);
        // MSSQL does not include the instance ID field in the SubOrchestrationInstanceCreatedEvent
        if (isNotMSSQL)
        {
            Assert.Equal(subOrchestrationInstanceId, subOrchestrationInstanceCreatedEvent.InstanceId);
        }
        Assert.Equal(EventType.OrchestratorCompleted, historyEvents[3].EventType);

        // OrchestratorStarted, SubOrchestrationInstanceCompleted, ExecutionCompleted, OrchestratorCompleted
        Assert.Equal(EventType.OrchestratorStarted, historyEvents[4].EventType);
        Assert.Equal(EventType.SubOrchestrationInstanceCompleted, historyEvents[5].EventType);
        Assert.Equal(EventType.ExecutionCompleted, historyEvents[6].EventType);
        Assert.Equal(EventType.OrchestratorCompleted, historyEvents[7].EventType);

        // Now confirm the fields of the SubOrchestrationInstanceCompleted and ExecutionCompleted events (the result, task ID, and absence of failure details)
        var executionCompletedEvent = (ExecutionCompletedEvent)historyEvents[6];
        var subOrchestrationCompletedEvent = (SubOrchestrationInstanceCompletedEvent)historyEvents[5];
        ComplexInput result = new("succeed", subOrchestrationInstanceId, OutputSize, isNotMSSQL, null);
        Assert.Null(executionCompletedEvent.FailureDetails);
        Assert.NotNull(executionCompletedEvent.Result);
        Assert.Equal(result, JsonConvert.DeserializeObject<ComplexInput>(executionCompletedEvent.Result));
        Assert.Equal(subOrchestrationInstanceCreatedEvent.EventId, subOrchestrationCompletedEvent.TaskScheduledId);
        Assert.Equal(result, JsonConvert.DeserializeObject<ComplexInput>(subOrchestrationCompletedEvent.Result));

        // Verify that the ClientOperationReceived logs were emitted with a FunctionInvocationId
        ClientOperationLogHelpers.AssertClientOperationLogExists(
            () => this.fixture.TestLogs.CoreToolsLogs,
            "StartOrchestration",
            instanceId,
            this.fixture.functionLanguageLocalizer.GetLanguageType());
        ClientOperationLogHelpers.AssertClientOperationLogExists(
            () => this.fixture.TestLogs.CoreToolsLogs,
            "StreamInstanceHistory",
            instanceId,
            this.fixture.functionLanguageLocalizer.GetLanguageType());

        // The suborchestration calls Activities/entities with large outputs, so it should force multiple history chunks in the streaming process
        using HttpResponseMessage getSubOrchestrationHistoryResponse = await HttpHelpers.InvokeHttpTrigger("GetInstanceHistory", $"?instanceId={subOrchestrationInstanceId}");
        Assert.Equal(HttpStatusCode.OK, getSubOrchestrationHistoryResponse.StatusCode);
        string subOrchestrationJsonHistory = await getSubOrchestrationHistoryResponse.Content.ReadAsStringAsync();
        List<HistoryEvent>? subOrchestrationHistoryEvents = JsonConvert.DeserializeObject<List<HistoryEvent>>(
            subOrchestrationJsonHistory,
            new JsonSerializerSettings()
            {
                Converters = { new HistoryEventJsonConverter() },
            });
        Assert.NotNull(subOrchestrationHistoryEvents);

        // Confirm the correct count and sequence of events for the suborchestration
        Assert.Equal(isNotMSSQL ? 17 : 12, subOrchestrationHistoryEvents.Count);

        // OrchestratorStarted, ExecutionStarted, TaskScheduled, OrchestratorCompleted
        Assert.Equal(EventType.OrchestratorStarted, subOrchestrationHistoryEvents[0].EventType);
        Assert.Equal(EventType.ExecutionStarted, subOrchestrationHistoryEvents[1].EventType);
        // Confirm the fields of the ExecutionStartedEvent for the suborchestration (name, orchestration input, parent information, task ID)
        var subOrchestrationExecutionStartedEvent = (ExecutionStartedEvent)subOrchestrationHistoryEvents[1];
        Assert.Equal("CallLargeOutputTasksSubOrchestration", subOrchestrationExecutionStartedEvent.Name);
        Assert.Equal(result, JsonConvert.DeserializeObject<ComplexInput>(subOrchestrationExecutionStartedEvent.Input));
        Assert.Equal(parentExecutionStartedEvent.OrchestrationInstance.InstanceId, subOrchestrationExecutionStartedEvent.ParentInstance.OrchestrationInstance.InstanceId);
        Assert.Equal(subOrchestrationInstanceCreatedEvent.EventId, subOrchestrationExecutionStartedEvent.ParentInstance.TaskScheduleId);
        // MSSQL currently only adds the instance ID and task scheduled ID fields to the parent instance object
        if (isNotMSSQL)
        {
            Assert.Equal("ParentOrchestration", subOrchestrationExecutionStartedEvent.ParentInstance.Name);
            Assert.Equal(parentExecutionStartedEvent.OrchestrationInstance.ExecutionId, subOrchestrationExecutionStartedEvent.ParentInstance.OrchestrationInstance.ExecutionId);
        }
        Assert.Equal(EventType.TaskScheduled, subOrchestrationHistoryEvents[2].EventType);
        Assert.Equal("LargeOutputActivity", ((TaskScheduledEvent)subOrchestrationHistoryEvents[2]).Name);
        Assert.Equal(EventType.OrchestratorCompleted, subOrchestrationHistoryEvents[3].EventType);

        // OrchestratorStarted, TaskCompleted
        Assert.Equal(EventType.OrchestratorStarted, subOrchestrationHistoryEvents[4].EventType);
        Assert.Equal(EventType.TaskCompleted, subOrchestrationHistoryEvents[5].EventType);
        var taskCompletedEvent = (TaskCompletedEvent)subOrchestrationHistoryEvents[5];
        // Confirm the event ID of the TaskScheduledEvent matches the TaskScheduledId field of the TaskCompletedEvent
        Assert.Equal(subOrchestrationHistoryEvents[2].EventId, taskCompletedEvent.TaskScheduledId);
        Assert.Equal($"\"{new string('a', OutputSize)}\"", taskCompletedEvent.Result);

        ExecutionCompletedEvent subOrchestrationExecutionCompletedEvent;
        if (isNotMSSQL)
        {
            // EventSentEvent, TimerCreated, OrchestratorCompleted
            Assert.Equal(EventType.EventSent, subOrchestrationHistoryEvents[6].EventType);
            Assert.Equal(entityId.ToString(), ((EventSentEvent)subOrchestrationHistoryEvents[6]).InstanceId);
            Assert.Equal(EventType.TimerCreated, subOrchestrationHistoryEvents[7].EventType);
            Assert.Equal(EventType.OrchestratorCompleted, subOrchestrationHistoryEvents[8].EventType);

            // OrchestratorStarted, TimerFired, EventSentEvent, OrchestratorCompleted
            Assert.Equal(EventType.OrchestratorStarted, subOrchestrationHistoryEvents[9].EventType);
            Assert.Equal(EventType.TimerFired, subOrchestrationHistoryEvents[10].EventType);
            // Confirm the event ID of the TimerCreatedEvent matches the TimerId field of the TimerFiredEvent
            Assert.Equal(subOrchestrationHistoryEvents[7].EventId, ((TimerFiredEvent)subOrchestrationHistoryEvents[10]).TimerId);
            Assert.Equal(EventType.EventSent, subOrchestrationHistoryEvents[11].EventType);
            Assert.Equal(entityId.ToString(), ((EventSentEvent)subOrchestrationHistoryEvents[11]).InstanceId);
            Assert.Equal(EventType.OrchestratorCompleted, subOrchestrationHistoryEvents[12].EventType);

            // OrchestratorStarted, EventRaised, ExecutionCompleted, OrchestratorCompleted
            Assert.Equal(EventType.OrchestratorStarted, subOrchestrationHistoryEvents[13].EventType);
            Assert.Equal(EventType.EventRaised, subOrchestrationHistoryEvents[14].EventType);
            Assert.Equal(EventType.ExecutionCompleted, subOrchestrationHistoryEvents[15].EventType);
            Assert.Equal(EventType.OrchestratorCompleted, subOrchestrationHistoryEvents[16].EventType);
            subOrchestrationExecutionCompletedEvent = (ExecutionCompletedEvent)subOrchestrationHistoryEvents[15];
        }
        else
        {
            // TaskScheduled, OrchestratorCompleted
            Assert.Equal(EventType.TaskScheduled, subOrchestrationHistoryEvents[6].EventType);
            Assert.Equal("LargeOutputActivity", ((TaskScheduledEvent)subOrchestrationHistoryEvents[6]).Name);
            Assert.Equal(EventType.OrchestratorCompleted, subOrchestrationHistoryEvents[7].EventType);

            // OrchestratorStarted, TaskCompleted, ExecutionCompleted, OrchestratorCompleted
            Assert.Equal(EventType.OrchestratorStarted, subOrchestrationHistoryEvents[8].EventType);
            taskCompletedEvent = (TaskCompletedEvent)subOrchestrationHistoryEvents[9];
            // Confirm the event ID of the TaskScheduledEvent matches the TaskScheduledId field of the TaskCompletedEvent
            Assert.Equal(subOrchestrationHistoryEvents[6].EventId, taskCompletedEvent.TaskScheduledId);
            Assert.Equal($"\"{new string('a', OutputSize)}\"", taskCompletedEvent.Result);
            Assert.Equal(EventType.ExecutionCompleted, subOrchestrationHistoryEvents[10].EventType);
            Assert.Equal(EventType.OrchestratorCompleted, subOrchestrationHistoryEvents[11].EventType);
            subOrchestrationExecutionCompletedEvent = (ExecutionCompletedEvent)subOrchestrationHistoryEvents[10];
        }

        // Confirm the details of the ExecutionCompleted event for the suborchestration (the result and absence of failure details)
        Assert.Null(subOrchestrationExecutionCompletedEvent.FailureDetails);
        Assert.NotNull(subOrchestrationExecutionCompletedEvent.Result);
        Assert.Equal(result, JsonConvert.DeserializeObject<ComplexInput>(subOrchestrationExecutionCompletedEvent.Result));
    }

    [Fact]
    [Trait("Java", "Skip")] // The GetOrchestrationHistory API is not implemented in Java
    [Trait("Python", "Skip")] // The GetOrchestrationHistory API is not implemented in Python
    [Trait("PowerShell", "Skip")] // The GetOrchestrationHistory API is not implemented in PowerShell
    [Trait("Node", "Skip")] // The GetOrchestrationHistory API is not implemented in Node
    public async Task GetOrchestrationHistory_InvalidInstanceId_ThrowsArgumentException()
    {
        string nonExistentInstanceId = Guid.NewGuid().ToString();
        // Try to get the history for a non-existent orchestration instance ID
        HttpResponseMessage getOrchestrationHistoryResponse = await HttpHelpers.InvokeHttpTrigger("GetInstanceHistory", $"?instanceId={nonExistentInstanceId}");
        Assert.Equal(HttpStatusCode.NotFound, getOrchestrationHistoryResponse.StatusCode);
        getOrchestrationHistoryResponse.Dispose();
        // Try to get the history for an entity instance ID
        getOrchestrationHistoryResponse = await HttpHelpers.InvokeHttpTrigger("GetInstanceHistory", $"?instanceId={entityId}");
        Assert.Equal(HttpStatusCode.NotFound, getOrchestrationHistoryResponse.StatusCode);
        getOrchestrationHistoryResponse.Dispose();
    }

    // Unfortunately something about building from the command line fails if we try to import this from the GetOrchestrationHistory class
    // (even though it builds just fine in Visual Studio). As such the GitHub pipelines fail.
    // For now we will just duplicate this class here.
    public class ComplexInput(
        string? orchestrationType,
        string subOrchestrationInstanceId,
        int outputSize,
        bool callEntities,
        Dictionary<string, string>? tags)
    {
        public bool CallEntities { get; set; } = callEntities;

        public string? OrchestrationType { get; set; } = orchestrationType;

        public string SubOrchestrationInstanceId { get; set; } = subOrchestrationInstanceId;

        public int OutputSize { get; set; } = outputSize;

        public Dictionary<string, string>? Tags { get; set; } = tags;

        public override bool Equals(object? obj)
        {
            if (obj is not ComplexInput other)
            {
                return false;
            }
            return other.CallEntities == this.CallEntities
                && ((other.OrchestrationType is null && this.OrchestrationType is null)
                || (other.OrchestrationType is not null && this.OrchestrationType is not null
                && other.OrchestrationType.Equals(this.OrchestrationType)))
                && other.SubOrchestrationInstanceId.Equals(this.SubOrchestrationInstanceId)
                && other.OutputSize == this.OutputSize
                && ((other.Tags is null && this.Tags is null)
                || (other.Tags is not null && this.Tags is not null
                && other.Tags.OrderBy(x => x.Key).SequenceEqual(this.Tags.OrderBy(x => x.Key))));
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.CallEntities, this.OrchestrationType, this.SubOrchestrationInstanceId, this.OutputSize, this.Tags);
        }
    }
}
