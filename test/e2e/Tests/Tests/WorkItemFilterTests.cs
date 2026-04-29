// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

/// <summary>
/// E2E tests for work item filtering with the AzureManaged (DTS) backend.
/// These tests verify that when workItemFilteringEnabled is true in host.json,
/// the DTS backend only dispatches work items for functions registered in this app.
/// Orchestrations for unknown function names should stay Pending instead of failing.
/// </summary>
[Collection(WorkItemFilterCollection.Name)]
[Trait("AzureStorage", "Skip")] // Work item filtering is a DTS-only feature
[Trait("MSSQL", "Skip")] // Work item filtering is a DTS-only feature
[Trait("DTS", "Skip")] // TODO: Remove once AzureManaged backend package with work item filter support is available
public class WorkItemFilterTests
{
    private readonly WorkItemFilterFixture fixture;
    private readonly ITestOutputHelper output;

    public WorkItemFilterTests(WorkItemFilterFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(testOutputHelper);
        this.output = testOutputHelper;
    }

    /// <summary>
    /// Verifies that a known orchestration (registered in this app) completes normally
    /// when work item filtering is enabled. This is the positive control — filters
    /// should not prevent dispatching matching work items.
    /// </summary>
    [Fact]
    public async Task KnownOrchestration_CompletesWithFiltering()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(
            "StartOrchestration",
            "?orchestrationName=HelloCities");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        var details = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        Assert.Equal("Completed", details.RuntimeStatus);
        Assert.Contains("Hello Tokyo!", details.Output);
    }

    /// <summary>
    /// Verifies that an unknown orchestration (NOT registered in this app) stays in
    /// Pending state when work item filtering is enabled. Without filtering, this would
    /// fail with "The function 'X' doesn't exist". With filtering, DTS holds the work
    /// item in the queue because no connected worker has it in its filter list.
    /// </summary>
    [Fact]
    public async Task UnknownOrchestration_StaysPendingWithFiltering()
    {
        string unknownName = $"NonExistentOrchestration_{Guid.NewGuid():N}";

        // Start a known orchestration alongside the unknown one so we can use
        // its completion as a synchronization signal — proving the worker is
        // actively dispatching work items — before asserting the unknown one
        // is still Pending.
        using HttpResponseMessage knownResponse = await HttpHelpers.InvokeHttpTrigger(
            "StartOrchestration",
            "?orchestrationName=HelloCities");

        using HttpResponseMessage unknownResponse = await HttpHelpers.InvokeHttpTrigger(
            "StartOrchestration",
            $"?orchestrationName={unknownName}");

        Assert.Equal(HttpStatusCode.Accepted, knownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknownResponse.StatusCode);

        string knownStatusUri = await DurableHelpers.ParseStatusQueryGetUriAsync(knownResponse);
        string unknownStatusUri = await DurableHelpers.ParseStatusQueryGetUriAsync(unknownResponse);

        // Wait for the known orchestration to complete — this proves the worker
        // is running and dispatching work items.
        await DurableHelpers.WaitForOrchestrationStateAsync(knownStatusUri, "Completed", 30);

        // Now assert the unknown orchestration is still Pending.
        // With filtering enabled: Pending (DTS holds it — no matching worker)
        // Without filtering: would be Failed ("function doesn't exist")
        var details = await DurableHelpers.GetRunningOrchestrationDetailsAsync(unknownStatusUri);
        Assert.Equal("Pending", details.RuntimeStatus);

        this.output.WriteLine(
            $"Unknown orchestration '{unknownName}' stayed Pending as expected (filter isolation working)");
    }

    /// <summary>
    /// Verifies that two different registered orchestration types both complete when filtering
    /// is enabled. This proves filters correctly include all registered functions, not just one.
    /// HelloCities calls SayHello activity; GetMainActivityInfoOrchestration calls a different
    /// activity — proving both orchestrator and activity filters work across function types.
    /// </summary>
    [Fact]
    public async Task DifferentKnownOrchestrations_BothCompleteWithFiltering()
    {
        // Start two different orchestration types
        using HttpResponseMessage response1 = await HttpHelpers.InvokeHttpTrigger(
            "StartOrchestration",
            "?orchestrationName=HelloCities");

        using HttpResponseMessage response2 = await HttpHelpers.InvokeHttpTrigger(
            "StartOrchestration",
            "?orchestrationName=GetMainActivityInfoOrchestration");

        Assert.Equal(HttpStatusCode.Accepted, response1.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, response2.StatusCode);

        string uri1 = await DurableHelpers.ParseStatusQueryGetUriAsync(response1);
        string uri2 = await DurableHelpers.ParseStatusQueryGetUriAsync(response2);

        // Both should complete — filters include all registered functions
        await DurableHelpers.WaitForOrchestrationStateAsync(uri1, "Completed", 30);
        await DurableHelpers.WaitForOrchestrationStateAsync(uri2, "Completed", 30);

        var details1 = await DurableHelpers.GetRunningOrchestrationDetailsAsync(uri1);
        var details2 = await DurableHelpers.GetRunningOrchestrationDetailsAsync(uri2);

        Assert.Equal("Completed", details1.RuntimeStatus);
        Assert.Equal("Completed", details2.RuntimeStatus);

        this.output.WriteLine($"Orchestration 1 output: {details1.Output}");
        this.output.WriteLine($"Orchestration 2 output: {details2.Output}");
    }
}
