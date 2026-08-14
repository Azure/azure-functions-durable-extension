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
    private readonly ITestOutputHelper output;

    public DisabledOrchestrationTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.output = testOutputHelper;
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

    [Fact]
    [Trait("PowerShell", "Skip")] // DisabledOrchestration is an actual disabled fixture in BasicDotNetIsolated only.
    [Trait("Python", "Skip")] // DisabledOrchestration is an actual disabled fixture in BasicDotNetIsolated only.
    [Trait("Node", "Skip")] // DisabledOrchestration is an actual disabled fixture in BasicDotNetIsolated only.
    [Trait("Java", "Skip")] // DisabledOrchestration is an actual disabled fixture in BasicDotNetIsolated only.
    public async Task DisabledOrchestration_HttpStartIsRejectedWithoutCreatingInstance()
    {
        string bootstrapInstanceId = $"bootstrap-{Guid.NewGuid():N}";
        using HttpResponseMessage bootstrapResponse = await HttpHelpers.InvokeHttpTrigger(
            "StartOrchestration",
            $"?orchestrationName=HelloCities&instanceId={bootstrapInstanceId}");
        Assert.Equal(HttpStatusCode.Accepted, bootstrapResponse.StatusCode);

        string bootstrapStatusUri = await DurableHelpers.ParseStatusQueryGetUriAsync(bootstrapResponse);
        Assert.False(string.IsNullOrEmpty(bootstrapStatusUri));

        string disabledInstanceId = $"disabled-{Guid.NewGuid():N}";
        Uri disabledStartUri = CreateManagementUri(
            bootstrapStatusUri,
            $"/orchestrators/DisabledOrchestration/{disabledInstanceId}");
        Uri disabledStatusUri = CreateManagementUri(
            bootstrapStatusUri,
            $"/instances/{disabledInstanceId}");

        using var client = new HttpClient();
        using HttpResponseMessage startResponse = await client.PostAsync(disabledStartUri, content: null);
        string startContent = await startResponse.Content.ReadAsStringAsync();
        (HttpStatusCode StatusCode, string Content) statusObservation =
            await ObserveInstanceAsync(client, disabledStatusUri, startResponse.StatusCode == HttpStatusCode.Accepted);

        this.output.WriteLine($"Disabled start response: {(int)startResponse.StatusCode} {startResponse.StatusCode}; body: {startContent}");
        this.output.WriteLine($"Disabled instance lookup: {(int)statusObservation.StatusCode} {statusObservation.StatusCode}; body: {statusObservation.Content}");

        Assert.Equal(HttpStatusCode.BadRequest, startResponse.StatusCode);
        Assert.Contains("doesn't exist, is disabled, or is not an orchestrator function", startContent);
        Assert.Equal(HttpStatusCode.NotFound, statusObservation.StatusCode);
    }

    [Fact]
    [Trait("PowerShell", "Skip")] // DisabledOrchestration is an actual disabled fixture in BasicDotNetIsolated only.
    [Trait("Python", "Skip")] // DisabledOrchestration is an actual disabled fixture in BasicDotNetIsolated only.
    [Trait("Node", "Skip")] // DisabledOrchestration is an actual disabled fixture in BasicDotNetIsolated only.
    [Trait("Java", "Skip")] // DisabledOrchestration is an actual disabled fixture in BasicDotNetIsolated only.
    public async Task DisabledOrchestration_DotNetIsolatedClientRejectsWithoutCreatingInstance()
    {
        string controlInstanceId = GetConfiguredInstanceId(
            "DURABLE_E2E_CONTROL_INSTANCE_ID",
            "control");
        using HttpResponseMessage controlStartResponse = await HttpHelpers.InvokeHttpTrigger(
            "StartOrchestration",
            $"?orchestrationName=HelloCities&instanceId={controlInstanceId}");
        string controlStartContent = await controlStartResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Accepted, controlStartResponse.StatusCode);

        string controlStatusUri = await DurableHelpers.ParseStatusQueryGetUriAsync(controlStartResponse);
        Assert.False(string.IsNullOrEmpty(controlStatusUri));
        await DurableHelpers.WaitForOrchestrationStateAsync(controlStatusUri, "Completed", 30);

        using var client = new HttpClient();
        using HttpResponseMessage controlStatusResponse = await client.GetAsync(controlStatusUri);
        string controlStatusContent = await controlStatusResponse.Content.ReadAsStringAsync();
        this.output.WriteLine($"Enabled control start: {(int)controlStartResponse.StatusCode} {controlStartResponse.StatusCode}; body: {controlStartContent}");
        this.output.WriteLine($"Enabled control lookup: {(int)controlStatusResponse.StatusCode} {controlStatusResponse.StatusCode}; body: {controlStatusContent}");
        Assert.Equal(HttpStatusCode.OK, controlStatusResponse.StatusCode);
        Assert.Contains("\"runtimeStatus\":\"Completed\"", controlStatusContent);

        string disabledInstanceId = GetConfiguredInstanceId(
            "DURABLE_E2E_DISABLED_INSTANCE_ID",
            "disabled");
        using HttpResponseMessage disabledStartResponse = await HttpHelpers.InvokeHttpTrigger(
            "StartOrchestration",
            $"?orchestrationName=DisabledOrchestration&instanceId={disabledInstanceId}");
        string disabledStartContent = await disabledStartResponse.Content.ReadAsStringAsync();
        Uri disabledStatusUri = CreateManagementUri(
            controlStatusUri,
            $"/instances/{disabledInstanceId}");
        (HttpStatusCode StatusCode, string Content) disabledStatusObservation = await ObserveInstanceAsync(
            client,
            disabledStatusUri,
            disabledStartResponse.StatusCode == HttpStatusCode.Accepted);

        this.output.WriteLine($"Disabled isolated-client start: {(int)disabledStartResponse.StatusCode} {disabledStartResponse.StatusCode}; body: {disabledStartContent}");
        this.output.WriteLine($"Disabled isolated-client lookup: {(int)disabledStatusObservation.StatusCode} {disabledStatusObservation.StatusCode}; body: {disabledStatusObservation.Content}");

        Assert.Equal(HttpStatusCode.InternalServerError, disabledStartResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, disabledStatusObservation.StatusCode);
        await this.fixture.TestLogs.AssertLogExistsAsync(
            log => log.Contains(
                    $"System.ArgumentException: Invalid argument for start instance request for instance ID {disabledInstanceId}",
                    StringComparison.Ordinal) &&
                log.Contains("DisabledOrchestration", StringComparison.Ordinal),
            "Expected the .NET isolated DurableTaskClient to reject DisabledOrchestration with ArgumentException.");
        await this.fixture.TestLogs.AssertLogExistsAsync(
            log => log.Contains(
                "GrpcDurableTaskClient.ScheduleNewOrchestrationInstanceAsync",
                StringComparison.Ordinal),
            "Expected the disabled start failure to originate from the gRPC DurableTaskClient.");
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

    private static string GetConfiguredInstanceId(string settingName, string prefix)
    {
        return Environment.GetEnvironmentVariable(settingName) ?? $"{prefix}-{Guid.NewGuid():N}";
    }

    private static Uri CreateManagementUri(string statusQueryGetUri, string relativeOperationPath)
    {
        var statusUri = new Uri(statusQueryGetUri);
        const string InstancesSegment = "/instances/";
        int instancesIndex = statusUri.AbsolutePath.LastIndexOf(InstancesSegment, StringComparison.OrdinalIgnoreCase);
        Assert.True(instancesIndex >= 0, $"Unexpected Durable status URI: {statusQueryGetUri}");

        var builder = new UriBuilder(statusUri)
        {
            Path = statusUri.AbsolutePath[..instancesIndex] + relativeOperationPath,
        };
        return builder.Uri;
    }

    private static async Task<(HttpStatusCode StatusCode, string Content)> ObserveInstanceAsync(
        HttpClient client,
        Uri statusUri,
        bool waitForExistence)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (true)
        {
            using HttpResponseMessage response = await client.GetAsync(statusUri);
            string content = await response.Content.ReadAsStringAsync();
            if (!waitForExistence || response.StatusCode != HttpStatusCode.NotFound || DateTime.UtcNow >= deadline)
            {
                return (response.StatusCode, content);
            }

            await Task.Delay(200);
        }
    }
}
