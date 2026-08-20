// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

// Validates behavior when disabled-but-still-deployed durable functions (DisabledOrchestration,
// DisabledActivity, DisabledEntity) are registered: the app starts and active orchestrations run
// normally, and scheduling work against a disabled activity/entity fails the orchestration
// deterministically instead of poison-looping forever (issue #3471).
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
    //
    // Runs on every language (each app defines a disabled DisabledActivity + a CallDisabledActivity
    // orchestrator). "DisabledActivity" is a substring of the caller orchestrator name, so it appears
    // in the failure output regardless of how each language formats it. MSSQL and DTS are skipped for
    // the same reason as ErrorHandlingTests.OrchestratorWithUncaughtActivityException_ShouldFail:
    // those backends don't reliably surface activity-failure output on the orchestration status
    // (durabletask-mssql#287 and the DTS work item linked there).
    [Fact]
    [Trait("MSSQL", "Skip")] // Activity-failure output is not surfaced on MSSQL: https://github.com/microsoft/durabletask-mssql/issues/287
    [Trait("DTS", "Skip")] // DTS will fail this test unless this bug is fixed: https://msazure.visualstudio.com/Antares/_workitems/edit/31779638
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
    //
    // Runs on the languages whose durable SDK supports entities (dotnet-isolated, Node, Python).
    // PowerShell and Java are skipped because durable entities are not implemented in those SDKs.
    // MSSQL is skipped because durable entities are not supported on that backend. DTS *does* support
    // entities, but — like the activity test above and
    // ErrorHandlingTests.OrchestratorWithUncaughtEntityException_ShouldFail — this test asserts on the
    // failure being surfaced in the orchestration status output, which DTS does not do reliably until
    // the referenced bug is fixed.
    [Fact]
    [Trait("MSSQL", "Skip")] // Durable entities are not supported on the MSSQL backend
    [Trait("DTS", "Skip")] // DTS supports entities, but doesn't surface the failure in status output until this bug is fixed: https://msazure.visualstudio.com/Antares/_workitems/edit/31779638
    [Trait("PowerShell", "Skip")] // Durable entities are not implemented in the PowerShell SDK
    [Trait("Java", "Skip")] // Durable entities are not implemented in the Java SDK
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
