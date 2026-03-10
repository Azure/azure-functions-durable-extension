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

    public IsReplayingTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(testOutputHelper);
    }

    [Fact]
    [Trait("Dotnet", "Skip")] // Replay behavior in dotnet ensures that orchestrator code only runs once per execution
    [Trait("Python-DTS", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-python/issues/595
    [Trait("Node-DTS", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-js/issues/677
    [Trait("PowerShell-DTS", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-powershell/issues/106
    public async Task IsReplayingBasic_CompletesWithExpectedReplayFlags()
    {
        /**
        The IsReplayingBasic orchestrator captures is_replaying before and after a single
        activity call (an echo activity with input "hello").

        On the final replay pass the code before the yield has already been seen, so
        is_replaying is true; the code after the yield is executing for the first time,
        so is_replaying is false.

        Expected output:
        {
            "before_activity": true,
            "after_activity":  false,
            "activity_result": "hello"
        }
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
        The IsReplayingMultiActivity orchestrator calls three sequential activities
        ("one", "two", "three") and records an is_replaying snapshot at four checkpoints:
        before the first activity, and after each of the three activities.

        On the final replay pass the first three checkpoints (start, after_first,
        after_second) are replaying because their corresponding activities have already
        completed. Only the last checkpoint (after_third) is fresh execution.

        Expected output:
        {
            "snapshots": [
                { "step": 0, "label": "start",        "is_replaying": true  },
                { "step": 1, "label": "after_first",  "is_replaying": true  },
                { "step": 2, "label": "after_second", "is_replaying": true  },
                { "step": 3, "label": "after_third",  "is_replaying": false }
            ],
            "activities": ["one", "two", "three"]
        }
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
    [Trait("Node", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-js/issues/564
    [Trait("Python-DTS", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-python/issues/595
    [Trait("Node-DTS", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-js/issues/677
    [Trait("PowerShell-DTS", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-powershell/issues/106
    public async Task IsReplayingConditionalLog_OnlyCountsLiveExecutionPaths()
    {
        /**
        The IsReplayingConditionalLog orchestrator uses is_replaying to guard logging.
        Before and after a single activity call (an echo activity with input "logged"),
        it checks is_replaying and only increments a live_log_count counter (and emits a
        log line) when is_replaying is false.

        On the final replay pass the pre-activity check is replaying (no increment), and
        the post-activity check is live (increments once). A log line
        "IsReplayingConditionalLog: LIVE after activity" is also emitted.

        Across all passes, "LIVE before activity" is emitted exactly once (on the first
        non-replay pass) and must not reappear on subsequent replay passes.

        Expected output:
        {
            "live_log_count":  1,
            "activity_result": "logged"
        }
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

        // Poll for the expected log line.
        const string liveAfterLog = "IsReplayingConditionalLog: LIVE after activity";
        const string liveBeforeLog = "IsReplayingConditionalLog: LIVE before activity";

        await this.fixture.TestLogs.AssertLogExistsAsync(l => l.Contains(liveAfterLog), $"Expected log line '{liveAfterLog}' was not found within the timeout.");

        // "LIVE before activity" is emitted on the first (non-replay) pass but NOT on the
        // final replay pass (where is_replaying is true), so it should appear exactly once.
        int liveBeforeCount = this.fixture.TestLogs.CoreToolsLogs
            .Count(l => l.Contains(liveBeforeLog));
        Assert.Equal(1, liveBeforeCount);
    }

    [Fact]
    [Trait("Dotnet", "Skip")] // Replay behavior in dotnet ensures that orchestrator code only runs once per execution
    [Trait("Python-DTS", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-python/issues/595
    [Trait("Node-DTS", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-js/issues/677
    [Trait("PowerShell-DTS", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-powershell/issues/106
    public async Task IsReplayingCounter_TracksReplayAndLiveCheckpoints()
    {
        /**
        The IsReplayingCounter orchestrator calls three sequential activities ("a", "b", "c")
        and tallies replay vs. non-replay checkpoint counts at four points: before the first
        activity, and after each of the three activities.

        On the final replay pass the first three checkpoints are replaying (replay_count = 3)
        and only the last checkpoint (after "c") is live (non_replay_count = 1),
        for a total of 4 checkpoints.

        Expected output:
        {
            "non_replay_count":  1,
            "replay_count":      3,
            "total_checkpoints": 4,
            "activities":        ["a", "b", "c"]
        }
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
        The IsReplayingFanOutFanIn orchestrator captures is_replaying before scheduling
        three parallel activities ("alpha", "beta", "gamma") and again after awaiting
        the fan-in (WhenAll / task_all).

        On the final replay pass the code before the fan-out is replaying (true) and
        the code after the fan-in is live (false). Activity results may appear in any
        order since they run in parallel.

        Expected output:
        {
            "before_fan_out": true,
            "after_fan_in":   false,
            "activities":     ["alpha", "beta", "gamma"]   // order may vary
        }
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
