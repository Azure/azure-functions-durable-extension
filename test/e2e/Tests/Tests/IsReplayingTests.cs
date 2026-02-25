// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class IsReplayingTests
{
    private readonly FunctionAppFixture fixture;
    private readonly ITestOutputHelper output;

    public IsReplayingTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(testOutputHelper);
        this.output = testOutputHelper;
    }

    [Fact]
    [Trait("Dotnet", "Skip")] // Replay behavior in dotnet ensures that orchestrator code only runs once per execution
    [Trait("Python-DTS", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-python/issues/595
    [Trait("Node-DTS", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-js/issues/677
    [Trait("PowerShell-DTS", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-powershell/issues/106
    public async Task IsReplayingBasic_CompletesWithExpectedReplayFlags()
    {
        /**
        Verifies a single-activity orchestrator reports is_replaying True before the activity 
        (during replay) and false after the activity (fresh execution).
        **/
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(
            "StartOrchestration", "?orchestrationName=IsReplayingBasic");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        var details = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        JsonNode outputJson = JsonNode.Parse(details.Output)!;

        // On the final replay pass, code before the activity yield is replaying,
        // and code after the activity yield runs fresh (not replaying).
        Assert.True(outputJson["before_activity"]!.GetValue<bool>(),
            "before_activity should be true (replaying on the final pass)");
        Assert.False(outputJson["after_activity"]!.GetValue<bool>(),
            "after_activity should be false (fresh execution after the last activity completes)");
        Assert.Equal("hello", outputJson["activity_result"]!.GetValue<string>());
    }

    [Fact]
    [Trait("Dotnet", "Skip")] // Replay behavior in dotnet ensures that orchestrator code only runs once per execution
    [Trait("Python-DTS", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-python/issues/595
    [Trait("Node-DTS", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-js/issues/677
    [Trait("PowerShell-DTS", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-powershell/issues/106
    public async Task IsReplayingMultiActivity_SnapshotsShowReplayProgression()
    {
        /**
        Verifies that a multi-activity orchestrator correctly reports is_replaying throughout execution, 
        showing the progression of replaying through multiple activities.
        **/
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(
            "StartOrchestration", "?orchestrationName=IsReplayingMultiActivity");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        var details = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        JsonNode outputJson = JsonNode.Parse(details.Output)!;

        // Verify activities completed correctly
        JsonArray activities = outputJson["activities"]!.AsArray();
        Assert.Equal(3, activities.Count);
        Assert.Equal("one", activities[0]!.GetValue<string>());
        Assert.Equal("two", activities[1]!.GetValue<string>());
        Assert.Equal("three", activities[2]!.GetValue<string>());

        // Verify snapshots: all checkpoints before the last activity are replaying;
        // only the final checkpoint (after the last activity) is not replaying.
        JsonArray snapshots = outputJson["snapshots"]!.AsArray();
        Assert.Equal(4, snapshots.Count);

        for (int i = 0; i < 3; i++)
        {
            Assert.True(snapshots[i]!["is_replaying"]!.GetValue<bool>(),
                $"Snapshot {i} ('{snapshots[i]!["label"]}') should be replaying");
        }

        Assert.False(snapshots[3]!["is_replaying"]!.GetValue<bool>(),
            "Final snapshot (after_third) should not be replaying");
    }

    [Fact]
    [Trait("Dotnet", "Skip")] // Replay behavior in dotnet ensures that orchestrator code only runs once per execution
    [Trait("Python-DTS", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-python/issues/595
    [Trait("Node-DTS", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-js/issues/677
    [Trait("PowerShell-DTS", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-powershell/issues/106
    public async Task IsReplayingConditionalLog_OnlyCountsLiveExecutionPaths()
    {
        /**
        Verifies code in an if-else statement only runs during live execution (not replay) 
        only runs once, using logging statements. 
        **/
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(
            "StartOrchestration", "?orchestrationName=IsReplayingConditionalLog");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        var details = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        JsonNode outputJson = JsonNode.Parse(details.Output)!;

        // On the final pass, only the code after the activity (non-replaying)
        // increments the live-log counter.
        Assert.Equal(1, outputJson["live_log_count"]!.GetValue<int>());
        Assert.Equal("logged", outputJson["activity_result"]!.GetValue<string>());

        await Task.Delay(2000);
        string logs = string.Join(Environment.NewLine, this.fixture.TestLogs.CoreToolsLogs);
        Assert.Contains("IsReplayingConditionalLog: LIVE after activity", logs);
    }

    [Fact]
    [Trait("Dotnet", "Skip")] // Replay behavior in dotnet ensures that orchestrator code only runs once per execution
    [Trait("Python-DTS", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-python/issues/595
    [Trait("Node-DTS", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-js/issues/677
    [Trait("PowerShell-DTS", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-powershell/issues/106
    public async Task IsReplayingCounter_TracksReplayAndLiveCheckpoints()
    {
        /**
        Validates is_replaying using both if/else and replay counts with a multi-activity orchestrator.
        **/
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(
            "StartOrchestration", "?orchestrationName=IsReplayingCounter");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        var details = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        JsonNode outputJson = JsonNode.Parse(details.Output)!;

        // 4 checkpoints (start + after each of 3 activities).
        // On the final pass, only the last checkpoint is live (non-replay).
        Assert.Equal(4, outputJson["total_checkpoints"]!.GetValue<int>());
        Assert.Equal(1, outputJson["non_replay_count"]!.GetValue<int>());
        Assert.Equal(3, outputJson["replay_count"]!.GetValue<int>());

        // Verify activity results
        JsonArray activities = outputJson["activities"]!.AsArray();
        Assert.Equal(3, activities.Count);
        Assert.Equal("a", activities[0]!.GetValue<string>());
        Assert.Equal("b", activities[1]!.GetValue<string>());
        Assert.Equal("c", activities[2]!.GetValue<string>());
    }

    [Fact]
    [Trait("Dotnet", "Skip")] // Replay behavior in dotnet ensures that orchestrator code only runs once per execution
    [Trait("Python-DTS", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-python/issues/595
    [Trait("Node", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-js/issues/679
    [Trait("Node-DTS", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-js/issues/677
    [Trait("PowerShell-DTS", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-powershell/issues/106
    public async Task IsReplayingFanOutFanIn_ReportsReplayStateAroundParallelTasks()
    {
        /**
        Validates is_replaying before/after a compound task (WhenAll)
        **/
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(
            "StartOrchestration", "?orchestrationName=IsReplayingFanOutFanIn");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        var details = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        JsonNode outputJson = JsonNode.Parse(details.Output)!;

        // Before fan-out is replaying (on the final pass); after fan-in is not.
        Assert.True(outputJson["before_fan_out"]!.GetValue<bool>(),
            "before_fan_out should be true (replaying on the final pass)");
        Assert.False(outputJson["after_fan_in"]!.GetValue<bool>(),
            "after_fan_in should be false (fresh execution after all tasks complete)");

        // Verify all activity results are present (order may vary for parallel execution)
        JsonArray activities = outputJson["activities"]!.AsArray();
        Assert.Equal(3, activities.Count);
        var activityValues = activities.Select(a => a!.GetValue<string>()).ToList();
        Assert.Contains("alpha", activityValues);
        Assert.Contains("beta", activityValues);
        Assert.Contains("gamma", activityValues);
    }
}
