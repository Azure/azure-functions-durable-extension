using System.Net;
using System.Text.Json;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionSequentialName)]
public class DedupeStatusesTests
{
    private readonly FunctionAppFixture fixture;
    private readonly ITestOutputHelper output;

    public DedupeStatusesTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(testOutputHelper);
        this.output = testOutputHelper;
    }

    [Fact]
    [Trait("Java", "Skip")] // Bug makes test flaky: https://github.com/microsoft/durabletask-java/issues/237
    public async Task CanStartOrchestration_WithSameId_ForAllStatuses_ForEmptyDedupeStatuses()
    {
        bool testTerminated = this.fixture.functionLanguageLocalizer.GetLanguageType() != LanguageType.Java
            || this.fixture.GetDurabilityProvider() != FunctionAppFixture.ConfiguredDurabilityProviderType.MSSQL;
        bool testPending = this.fixture.functionLanguageLocalizer.GetLanguageType() == LanguageType.DotnetIsolated
            || this.fixture.functionLanguageLocalizer.GetLanguageType() == LanguageType.Java;

        // HttpLongRunningOrchestrator (timer-based, no activity spam) is only available in dotnet-isolated.
        // For other languages, LongRunningOrchestrator is used for this test scenario.
        string longRunningOrch = this.fixture.functionLanguageLocalizer.GetLanguageType() == LanguageType.DotnetIsolated
            ? "HttpLongRunningOrchestrator"
            : "LongRunningOrchestrator";

        string completedId = Guid.NewGuid().ToString();
        string failedId = Guid.NewGuid().ToString();
        string terminatedId = Guid.NewGuid().ToString();
        string runningId = Guid.NewGuid().ToString();
        string suspendedId = Guid.NewGuid().ToString();
        string pendingId = Guid.NewGuid().ToString();

        // Phase 1: Start all first-attempt orchestrations and wait for initial states concurrently
        // Completed
        var completedFirst = StartAndWaitForState("HelloCities", completedId, "Completed");
        // Failed — "LargeOutputOrchestrator" expects a non-zero input, but we provide none
        var failedFirst = StartAndWaitForState("LargeOutputOrchestrator", failedId, "Failed");
        // Running
        var runningFirst = StartAndWaitForState(longRunningOrch, runningId, "Running");
        // Suspended (starts as Running, transitioned in Phase 2)
        var suspendedFirst = StartAndWaitForState(longRunningOrch, suspendedId, "Running");
        // Terminated (starts as Running, transitioned in Phase 2)
        var terminatedFirst = testTerminated
            ? StartAndWaitForState(longRunningOrch, terminatedId, "Running")
            : Task.FromResult<HttpResponseMessage>(null!);
        // Pending — scheduled start times are currently only implemented in Java and .NET isolated,
        // which is the only true way to get an orchestration in a "Pending" state
        var pendingFirst = testPending
            ? StartAndWaitForState("HelloCities", pendingId, "Pending", scheduledStartTime: DateTime.UtcNow.AddMinutes(10))
            : Task.FromResult<HttpResponseMessage>(null!);
        await Task.WhenAll(completedFirst, failedFirst, runningFirst, suspendedFirst, terminatedFirst, pendingFirst);

        // Phase 2: Apply state transitions concurrently
        var transitions = new List<Task>();
        if (testTerminated)
            transitions.Add(TerminateAndWaitForState(terminatedId, await terminatedFirst));
        transitions.Add(SuspendAndWaitForState(suspendedId, await suspendedFirst));
        await Task.WhenAll(transitions);

        // Dispose Phase 1 responses (no longer needed after extracting statusQueryGetUri)
        (await completedFirst)?.Dispose();
        (await failedFirst)?.Dispose();
        (await runningFirst)?.Dispose();
        (await suspendedFirst)?.Dispose();
        (await terminatedFirst)?.Dispose();
        (await pendingFirst)?.Dispose();

        // Phase 3: Start all second-attempt orchestrations concurrently (verify restart works)
        var phase3Tasks = new List<Task<HttpResponseMessage>>
        {
            // Completed
            StartAndWaitForState("HelloCities", completedId, "Completed"),
            // Failed — "LargeOutputOrchestrator" expects a non-zero input, but we provide none
            StartAndWaitForState("LargeOutputOrchestrator", failedId, "Failed"),
            // Running
            StartAndWaitForState(longRunningOrch, runningId, "Running"),
            // Suspended
            StartAndWaitForState(longRunningOrch, suspendedId, "Running"),
        };
        // Terminated
        if (testTerminated)
            phase3Tasks.Add(StartAndWaitForState(longRunningOrch, terminatedId, "Running"));
        // Pending
        if (testPending)
            phase3Tasks.Add(StartAndWaitForState("HelloCities", pendingId, "Completed"));
        foreach (var r in await Task.WhenAll(phase3Tasks))
            r?.Dispose();

        // Phase 4: Clean up non-terminal orchestrations concurrently
        var cleanups = new List<Task<HttpResponseMessage>>
        {
            HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={runningId}"),
            HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={suspendedId}"),
        };
        if (testTerminated)
            cleanups.Add(HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={terminatedId}"));
        if (testPending)
            cleanups.Add(HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={pendingId}"));
        foreach (var r in await Task.WhenAll(cleanups))
        {
            using (r)
            {
                if (r.StatusCode != HttpStatusCode.OK)
                {
                    this.output.WriteLine(
                        $"TerminateInstance cleanup returned status code {r.StatusCode} for request {r.RequestMessage?.RequestUri}");
                }
            }
        }
    }

    [Theory]
    [Trait("PowerShell", "Skip")] // Dedupe statuses not implemented in PowerShell
    [Trait("Python", "Skip")] // Dedupe statuses not implemented in Python
    [Trait("Node", "Skip")] // Dedupe statuses not implemented in Node
    [Trait("Java", "Skip")] // Dedupe statuses not implemented in Java
    [InlineData([])]
    [InlineData("Pending", "Failed")]
    public async Task StartOrchestration_WithSameId_FailsIfExistingStatus_InDedupeStatuses(params string[] dedupeStatuses)
    {
        // This test is dotnet-isolated only (Java/PowerShell/Python/Node are skipped via traits)
        string longRunningOrch = "HttpLongRunningOrchestrator";

        string completedId = Guid.NewGuid().ToString();
        string failedId = Guid.NewGuid().ToString();
        string terminatedId = Guid.NewGuid().ToString();
        string runningId = Guid.NewGuid().ToString();
        string suspendedId = Guid.NewGuid().ToString();
        string pendingId = Guid.NewGuid().ToString();

        // Phase 1: Start all first-attempt orchestrations concurrently
        // Completed
        var completedFirst = StartAndWaitForStateWithDedupeStatuses("HelloCities", completedId, "Completed", dedupeStatuses);
        // Failed — "LargeOutputOrchestrator" expects a non-zero input, but we provide none
        var failedFirst = StartAndWaitForStateWithDedupeStatuses("LargeOutputOrchestrator", failedId, "Failed", dedupeStatuses);
        // Terminated (starts as Running, transitioned in Phase 2)
        var terminatedFirst = StartAndWaitForStateWithDedupeStatuses(longRunningOrch, terminatedId, "Running", dedupeStatuses);
        // Running
        var runningFirst = StartAndWaitForStateWithDedupeStatuses(longRunningOrch, runningId, "Running", dedupeStatuses);
        // Suspended (starts as Running, transitioned in Phase 2)
        var suspendedFirst = StartAndWaitForStateWithDedupeStatuses(longRunningOrch, suspendedId, "Running", dedupeStatuses);
        // Pending
        var pendingFirst = StartAndWaitForStateWithDedupeStatuses(
            "HelloCities", pendingId, "Pending", dedupeStatuses, scheduledStartTime: DateTime.UtcNow.AddMinutes(10));
        await Task.WhenAll(completedFirst, failedFirst, terminatedFirst, runningFirst, suspendedFirst, pendingFirst);

        // Phase 2: Apply state transitions concurrently
        await Task.WhenAll(
            TerminateAndWaitForState(terminatedId, await terminatedFirst),
            SuspendAndWaitForState(suspendedId, await suspendedFirst));

        // Dispose Phase 1 responses
        foreach (var t in new Task<HttpResponseMessage>[] { completedFirst, failedFirst, terminatedFirst, runningFirst, suspendedFirst, pendingFirst })
            (await t)?.Dispose();

        // Phase 3: Start all second-attempt orchestrations concurrently (check dedupe behavior)
        var phase3 = await Task.WhenAll(
            // Completed
            StartAndWaitForStateWithDedupeStatuses("HelloCities", completedId, "Completed", dedupeStatuses,
                expectedCode: dedupeStatuses.Contains("Completed") ? HttpStatusCode.Conflict : HttpStatusCode.Accepted),
            // Failed — "LargeOutputOrchestrator" expects a non-zero input, but we provide none
            StartAndWaitForStateWithDedupeStatuses("LargeOutputOrchestrator", failedId, "Failed", dedupeStatuses,
                expectedCode: dedupeStatuses.Contains("Failed") ? HttpStatusCode.Conflict : HttpStatusCode.Accepted),
            // Terminated
            StartAndWaitForStateWithDedupeStatuses(longRunningOrch, terminatedId, "Running", dedupeStatuses,
                expectedCode: dedupeStatuses.Contains("Terminated") ? HttpStatusCode.Conflict : HttpStatusCode.Accepted),
            // Running
            StartAndWaitForStateWithDedupeStatuses(longRunningOrch, runningId, "Running", dedupeStatuses,
                expectedCode: dedupeStatuses.Contains("Running") ? HttpStatusCode.Conflict : HttpStatusCode.Accepted),
            // Suspended
            StartAndWaitForStateWithDedupeStatuses(longRunningOrch, suspendedId, "Running", dedupeStatuses,
                expectedCode: dedupeStatuses.Contains("Suspended") ? HttpStatusCode.Conflict : HttpStatusCode.Accepted),
            // Pending
            StartAndWaitForStateWithDedupeStatuses("HelloCities", pendingId, "Completed", dedupeStatuses,
                expectedCode: dedupeStatuses.Contains("Pending") ? HttpStatusCode.Conflict : HttpStatusCode.Accepted));
        foreach (var r in phase3)
            r?.Dispose();

        // Phase 4: Clean up running orchestrations concurrently
        var cleanups = new List<Task<HttpResponseMessage>>
        {
            HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={runningId}"),
            HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={suspendedId}"),
            HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={pendingId}"),
        };
        if (!dedupeStatuses.Contains("Terminated"))
            cleanups.Add(HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={terminatedId}"));
        foreach (var r in await Task.WhenAll(cleanups))
        {
            using (r)
            {
                if (r.StatusCode != HttpStatusCode.OK)
                {
                    this.output.WriteLine(
                        $"TerminateInstance cleanup returned status code {r.StatusCode} for request {r.RequestMessage?.RequestUri}");
                }
            }
        }
    }

    [Theory]
    [Trait("PowerShell", "Skip")] // Dedupe statuses not implemented in PowerShell
    [Trait("Python", "Skip")] // Dedupe statuses not implemented in Python
    [Trait("Node", "Skip")] // Dedupe statuses not implemented in Node
    [Trait("Java", "Skip")] // Dedupe statuses not implemented in Java
    [InlineData("Pending", "Failed", "Terminated")]
    [InlineData("Running", "Failed", "Terminated")]
    [InlineData("Suspended", "Failed", "Terminated")]
    public async Task StartOrchestration_WithInvalidDedupeStatuses_ThrowsArgumentException(params string[] dedupeStatuses)
    {
        // Dedupe statuses cannot have both "Terminated" and a running status
        // We do not provide an expected state since we expect the request to fail
        using HttpResponseMessage failedRequest = await StartAndWaitForStateWithDedupeStatuses(
            "HelloCities", Guid.NewGuid().ToString(), expectedState: string.Empty, dedupeStatuses, expectedCode: HttpStatusCode.BadRequest);
    }

    private async Task<HttpResponseMessage> StartAndWaitForState(
        string orchestrationName,
        string instanceId,
        string expectedState,
        DateTime? scheduledStartTime = null)
    {
        string functionName = "StartOrchestration";
        string queryString = $"?orchestrationName={orchestrationName}&instanceId={instanceId}";

        if (scheduledStartTime is not null)
        {
            queryString += $"&ScheduledStartTime={scheduledStartTime:o}";
            functionName = "HelloCities_HttpStart_Scheduled";
        }

        HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(functionName, queryString);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, expectedState, 60);

        if (expectedState != "Pending")
        {
            ClientOperationLogHelpers.AssertClientOperationLogExists(
            () => this.fixture.TestLogs.CoreToolsLogs,
            "StartOrchestration",
            instanceId,
            this.fixture.functionLanguageLocalizer.GetLanguageType());
        }

        return response;
    }

    private async Task<HttpResponseMessage> StartAndWaitForStateWithDedupeStatuses(
        string orchestrationName,
        string instanceId,
        string expectedState,
        string[] dedupeStatuses,
        DateTime? scheduledStartTime = null,
        HttpStatusCode expectedCode = HttpStatusCode.Accepted)
    {
        string queryString = $"?orchestrationName={orchestrationName}&instanceId={instanceId}" +
            $"&dedupeStatuses={JsonSerializer.Serialize(dedupeStatuses)}";

        if (scheduledStartTime is not null)
        {
            queryString += $"&scheduledStartTime={scheduledStartTime:o}";
        }

        HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration_DedupeStatuses", queryString);
        Assert.Equal(expectedCode, response.StatusCode);
        if (expectedCode != HttpStatusCode.Accepted)
        {
            return response;
        }
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, expectedState, 60);

        if (expectedState != "Pending")
        {
            ClientOperationLogHelpers.AssertClientOperationLogExists(
                () => this.fixture.TestLogs.CoreToolsLogs,
                "StartOrchestration",
                instanceId,
                this.fixture.functionLanguageLocalizer.GetLanguageType());
        }

        return response;
    }

    private async Task TerminateAndWaitForState(string instanceId, HttpResponseMessage startOrchestrationResponse)
    {
        using HttpResponseMessage terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={instanceId}");
        Assert.Equal(HttpStatusCode.OK, terminateResponse.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(startOrchestrationResponse);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Terminated", 60);

        ClientOperationLogHelpers.AssertClientOperationLogExists(
            () => this.fixture.TestLogs.CoreToolsLogs,
            "Terminate",
            instanceId,
            this.fixture.functionLanguageLocalizer.GetLanguageType());
    }

    private async Task SuspendAndWaitForState(string instanceId, HttpResponseMessage startOrchestrationResponse)
    {
        using HttpResponseMessage suspendResponse = await HttpHelpers.InvokeHttpTrigger("SuspendInstance", $"?instanceId={instanceId}");
        Assert.Equal(HttpStatusCode.OK, suspendResponse.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(startOrchestrationResponse);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Suspended", 60);

        ClientOperationLogHelpers.AssertClientOperationLogExists(
            () => this.fixture.TestLogs.CoreToolsLogs,
            "Suspend",
            instanceId,
            this.fixture.functionLanguageLocalizer.GetLanguageType());
    }
}
