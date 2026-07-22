// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

// Validates that the app starts and active orchestrations run normally when
// disabled durable functions (DisabledOrchestration, DisabledActivity,
// DisabledEntity) are also registered.
[Collection(Constants.FunctionAppCollectionName)]
public class DisabledOrchestrationTests
{
    private readonly FunctionAppFixture fixture;

    public DisabledOrchestrationTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(testOutputHelper);
    }

    [Fact]
    public async Task ActiveOrchestration_StartsWhenDisabledFunctionsAreRegistered()
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

    // Reproduces https://github.com/Azure/azure-functions-durable-extension/issues/3471: an
    // orchestration that schedules a disabled-but-still-deployed activity must fail deterministically
    // instead of poison-looping forever. Before the fix the activity dispatch threw during shim
    // construction / execution, which DTFx treated as a transient error and retried indefinitely, so
    // the orchestration stayed Running forever (this test would time out waiting for "Failed").
    [Fact]
    public async Task Orchestration_CallingDisabledActivity_FailsGracefully()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(
            "StartOrchestration",
            "?orchestrationName=CallDisabledActivity");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        // Must reach a terminal Failed state promptly; a poison loop would leave it Running.
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Failed", 30);

        var details = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        Assert.Equal("Failed", details.RuntimeStatus);
        Assert.Contains("DisabledActivity", details.Output);
    }

    // Companion to the activity test above, for the entity dispatch path: calling an operation on a
    // disabled-but-still-deployed entity must fail the orchestration deterministically rather than
    // poison-looping the entity work item forever.
    [Fact]
    public async Task Orchestration_CallingDisabledEntity_FailsGracefully()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(
            "StartOrchestration",
            "?orchestrationName=CallDisabledEntity");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Failed", 30);

        var details = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        Assert.Equal("Failed", details.RuntimeStatus);
        Assert.Contains("DisabledEntity", details.Output);
    }
}
