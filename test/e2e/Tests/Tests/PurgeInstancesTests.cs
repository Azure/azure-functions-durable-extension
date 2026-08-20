using System.Net;
using Microsoft.DurableTask.Entities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionSequentialName)]
public class PurgeInstancesTests
{
    private readonly FunctionAppFixture fixture;
    private readonly ITestOutputHelper output;

    public PurgeInstancesTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(testOutputHelper);
        this.output = testOutputHelper;
    }

    [Fact]
    [Trait("PowerShell", "Skip")] // Instance purging not supported in PowerShell
    [Trait("Python-DTS", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-python/issues/563
    [Trait("Node-DTS", "Skip")] // Bug: https://msazure.visualstudio.com/Antares/_workitems/edit/33910424/
    public async Task PurgeOrchestrationHistory_StartAndEnd_Succeeds()
    {
        // Previously this test used DateTime.MinValue - however, in Python on Linux specifically,
        // there is an issue where 0000-00-01 is not a valid date and the API throws. Should probably fix this (?)
        DateTime purgeStartTime = DateTime.UtcNow - TimeSpan.FromDays(365);
        DateTime purgeEndTime = DateTime.UtcNow;
        string queryParams = $"?purgeStartTime={purgeStartTime:o}&purgeEndTime={purgeEndTime:o}";
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("PurgeOrchestrationHistory", queryParams);
        string actualMessage = await response.Content.ReadAsStringAsync();
        Assert.Matches(@"^Purged [0-9]* records$", actualMessage);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("PowerShell", "Skip")] // Instance purging not supported in PowerShell
    [Trait("Python-DTS", "Skip")] // Bug: https://github.com/Azure/azure-functions-durable-python/issues/563
    [Trait("Node-DTS", "Skip")] // Bug: https://msazure.visualstudio.com/Antares/_workitems/edit/33910424/
    public async Task PurgeOrchestrationHistory_Start_Succeeds()
    {
        // Previously this test used DateTime.MinValue - however, in Python on Linux specifically,
        // there is an issue where 0000-00-01 is not a valid date and the API throws. Should probably fix this (?)
        DateTime purgeStartTime = DateTime.UtcNow - TimeSpan.FromDays(365);
        DateTime purgeEndTime = DateTime.UtcNow;
        string queryParams = $"?purgeStartTime={purgeStartTime:o}";
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("PurgeOrchestrationHistory", queryParams);
        string actualMessage = await response.Content.ReadAsStringAsync();
        Assert.Matches(@"^Purged [0-9]* records$", actualMessage);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("DTS", "Skip")] // Skip this test as there is a bug with current DTS backend, the createdTimeTo couldn't be null. 
    [Trait("PowerShell", "Skip")] // Instance purging not supported in PowerShell
    [Trait("Python", "Skip")] // Bug: purging without start time in Python: https://github.com/Azure/azure-functions-durable-python/issues/560
    [Trait("Node", "Skip")] // Bug: purging without start time in Node: https://github.com/Azure/azure-functions-durable-js/issues/644
    [Trait("Java", "Skip")] // Bug: purging without start time in Java: https://github.com/Azure/azure-functions-durable-js/issues/644
    public async Task PurgeOrchestrationHistory_End_Succeeds()
    {
        DateTime purgeEndTime = DateTime.UtcNow;
        string queryParams = $"?purgeEndTime={purgeEndTime:o}";
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("PurgeOrchestrationHistory", queryParams);
        string actualMessage = await response.Content.ReadAsStringAsync();
        Assert.Matches(@"^Purged [0-9]* records$", actualMessage);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("DTS", "Skip")] // Skip this test as there is a bug with current DTS backend, the createdTimeTo couldn't be null. 
    [Trait("PowerShell", "Skip")] // Instance purging not supported in PowerShell
    [Trait("Python", "Skip")] // Bug: purging without start time in Python: https://github.com/Azure/azure-functions-durable-python/issues/560
    [Trait("Node", "Skip")] // Bug: purging without start time in Node: https://github.com/Azure/azure-functions-durable-js/issues/644
    [Trait("Java", "Skip")] // Bug: purging without start time in Java: https://github.com/Azure/azure-functions-durable-js/issues/644
    public async Task PurgeOrchestrationHistory_NoBoundaries_Succeeds()
    {
        string queryParams = $"";
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("PurgeOrchestrationHistory", queryParams);
        string actualMessage = await response.Content.ReadAsStringAsync();
        Assert.Matches(@"^Purged [0-9]* records$", actualMessage);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("DTS", "Skip")] // Skip this test as there is a bug with current DTS backend, the createdTimeTo couldn't be null. 
    [Trait("PowerShell", "Skip")] // Instance purging not supported in PowerShell
    [Trait("Python", "Skip")] // Bug: purging without start time in Python: https://github.com/Azure/azure-functions-durable-python/issues/560
    [Trait("Node", "Skip")] // Bug: purging without start time in Node: https://github.com/Azure/azure-functions-durable-js/issues/644
    [Trait("Java", "Skip")] // Bug: purging without start time in Java: https://github.com/Azure/azure-functions-durable-js/issues/644
    public async Task PurgeOrchestrationHistoryAfterInvocation_Succeeds()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=HelloCities");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        DateTime purgeEndTime = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        using HttpResponseMessage purgeResponse = await HttpHelpers.InvokeHttpTrigger("PurgeOrchestrationHistory", $"?purgeEndTime={purgeEndTime:o}");
        string purgeMessage = await purgeResponse.Content.ReadAsStringAsync();
        Assert.Matches(@"^Purged [0-9]* records$", purgeMessage);
        Assert.DoesNotMatch(@"^Purged 0 records$", purgeMessage);
        Assert.Equal(HttpStatusCode.OK, purgeResponse.StatusCode);
    }

    [Fact]
    [Trait("DTS", "Skip")] // Skip this test as there is a bug with current DTS backend, the createdTimeTo couldn't be null. 
    [Trait("PowerShell", "Skip")] // Instance purging not supported in PowerShell
    [Trait("Python", "Skip")] // Bug: purging without start time in Python: https://github.com/Azure/azure-functions-durable-python/issues/560
    [Trait("Node", "Skip")] // Bug: purging without start time in Node: https://github.com/Azure/azure-functions-durable-js/issues/644
    [Trait("Java", "Skip")] // Bug: purging without start time in Java: https://github.com/Azure/azure-functions-durable-js/issues/644
    public async Task PurgeAfterPurge_ZeroRows()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=HelloCities");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        DateTime purgeEndTime = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        using HttpResponseMessage purgeResponse = await HttpHelpers.InvokeHttpTrigger("PurgeOrchestrationHistory", $"?purgeEndTime={purgeEndTime:o}");
        string purgeMessage = await purgeResponse.Content.ReadAsStringAsync();
        Assert.Matches(@"^Purged [0-9]* records$", purgeMessage);
        using HttpResponseMessage purgeAgainResponse = await HttpHelpers.InvokeHttpTrigger("PurgeOrchestrationHistory", $"?purgeEndTime={purgeEndTime:o}");
        Assert.Equal(HttpStatusCode.OK, purgeAgainResponse.StatusCode);
        await AssertPurgeCount(purgeAgainResponse, 0);
    }

    [Fact]
    [Trait("PowerShell", "Skip")] // Instance purging not supported in PowerShell
    [Trait("Java", "Skip")] // Bug: https://github.com/microsoft/durabletask-java/issues/237
    public async Task PurgeOnlyPurgesTerminalOrchestrations()
    {
        // For all of the following tests, since non-.NET languages throw a generic error in the case of a failure to purge there is no great way
        // to return specific status codes, whereas .NET isolated returns specific error types which can be used to return specific status codes.
        // So, in the non-.NET case, we simply check for the InternalServerError status code.
        void AssertFailedPurgeResponseStatusCode(HttpResponseMessage purgeHttpResponse)
        {
            if (this.fixture.functionLanguageLocalizer.GetLanguageType() == LanguageType.DotnetIsolated)
            {
                Assert.Equal(HttpStatusCode.PreconditionFailed, purgeHttpResponse.StatusCode);
            }
            else
            {
                Assert.Equal(HttpStatusCode.InternalServerError, purgeHttpResponse.StatusCode);
            }
        }

        bool testTerminated = this.fixture.functionLanguageLocalizer.GetLanguageType() != LanguageType.Java
            || this.fixture.GetDurabilityProvider() != FunctionAppFixture.ConfiguredDurabilityProviderType.MSSQL;
        bool testPending = this.fixture.functionLanguageLocalizer.GetLanguageType() == LanguageType.DotnetIsolated
            || this.fixture.functionLanguageLocalizer.GetLanguageType() == LanguageType.Java;

        // HttpLongRunningOrchestrator (timer-based, no activity spam) is only available in dotnet-isolated.
        // For other languages, LongRunningOrchestrator is used, which generates activity load against the
        // configured durability provider.
        string longRunningOrch = this.fixture.functionLanguageLocalizer.GetLanguageType() == LanguageType.DotnetIsolated
            ? "HttpLongRunningOrchestrator"
            : "LongRunningOrchestrator";

        // Phase 1: Start all orchestrations and wait for initial states concurrently
        // Completed orchestration, should succeed purge
        var completedStart = StartOrchAndWaitForStatus("HelloCities", "Completed");
        // Failed orchestration, should succeed purge
        var failedStart = StartOrchAndWaitForStatus("HelloActivityDIFailure", "Failed");
        // Terminated orchestration, should succeed purge
        var terminatedStart = testTerminated ? StartOrchAndWaitForStatus(longRunningOrch, "Running") : null;
        // Running orchestration, should fail purge
        var runningStart = StartOrchAndWaitForStatus(longRunningOrch, "Running");
        // Suspended orchestration, should fail purge
        var suspendedStart = StartOrchAndWaitForStatus(longRunningOrch, "Running");
        // Pending orchestration, should fail purge
        // Scheduled start times are currently only implemented in Java and .NET isolated,
        // which is the only true way to get an orchestration in a "Pending" state
        Task<(string instanceId, string statusUri)>? pendingStart = testPending
            ? StartOrchAndWaitForStatus("HelloCities", "Pending", scheduledStartTime: DateTime.UtcNow + TimeSpan.FromMinutes(1))
            : null;

        var phase1Tasks = new List<Task> { completedStart, failedStart, runningStart, suspendedStart };
        if (terminatedStart != null) phase1Tasks.Add(terminatedStart);
        if (pendingStart != null) phase1Tasks.Add(pendingStart);
        await Task.WhenAll(phase1Tasks);

        // Phase 2: Apply transitions concurrently (terminate, suspend)
        var transitions = new List<Task>();
        if (testTerminated)
        {
            var (termId, termUri) = await terminatedStart!;
            transitions.Add(TerminateAndWaitForStatus(termId, termUri));
        }
        var (suspId, suspUri) = await suspendedStart;
        transitions.Add(SuspendAndWaitForStatus(suspId, suspUri));
        await Task.WhenAll(transitions);

        // Phase 3: Test purge behavior — terminal states should succeed, non-terminal should fail
        var (completedId, _) = await completedStart;
        var (failedId, _) = await failedStart;
        var (runningId, _) = await runningStart;

        // Terminal state purges (can run concurrently)
        var terminalPurgeTasks = new List<Task>();
        terminalPurgeTasks.Add(AssertPurgeSucceeds(completedId));
        if (testTerminated)
            terminalPurgeTasks.Add(AssertPurgeSucceeds((await terminatedStart!).instanceId));
        terminalPurgeTasks.Add(AssertPurgeSucceeds(failedId));
        await Task.WhenAll(terminalPurgeTasks);

        // Non-existent orchestration, should succeed and have purge count of 0
        using HttpResponseMessage purgeNonExistent = await HttpHelpers.InvokeHttpTrigger("PurgeOrchestrationHistory", $"?instanceId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.OK, purgeNonExistent.StatusCode);
        await AssertPurgeCount(purgeNonExistent, 0);

        // Non-terminal state purges should fail (can run concurrently)
        var nonTerminalPurgeTasks = new List<Task<HttpResponseMessage>>
        {
            HttpHelpers.InvokeHttpTrigger("PurgeOrchestrationHistory", $"?instanceId={runningId}"),
            HttpHelpers.InvokeHttpTrigger("PurgeOrchestrationHistory", $"?instanceId={suspId}"),
        };
        if (testPending)
            nonTerminalPurgeTasks.Add(HttpHelpers.InvokeHttpTrigger("PurgeOrchestrationHistory", $"?instanceId={(await pendingStart!).instanceId}"));
        var nonTerminalResponses = await Task.WhenAll(nonTerminalPurgeTasks);
        foreach (var response in nonTerminalResponses)
        {
            using (response)
            {
                AssertFailedPurgeResponseStatusCode(response);
            }
        }

        // Verify that the ClientOperationReceived logs were emitted with a FunctionInvocationId
        ClientOperationLogHelpers.AssertClientOperationLogExists(
            () => this.fixture.TestLogs.CoreToolsLogs,
            "StartOrchestration",
            completedId,
            this.fixture.functionLanguageLocalizer.GetLanguageType());
        ClientOperationLogHelpers.AssertClientOperationLogExists(
            () => this.fixture.TestLogs.CoreToolsLogs,
            "PurgeInstances",
            completedId,
            this.fixture.functionLanguageLocalizer.GetLanguageType());

        // Best-effort cleanup of non-terminal instances to avoid background load on subsequent tests.
        // Terminate may return non-OK for already-completed or purged instances; log and dispose.
        var cleanups = new List<Task<HttpResponseMessage>>
        {
            HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={runningId}"),
            HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={suspId}"),
        };
        if (testPending)
            cleanups.Add(HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={(await pendingStart!).instanceId}"));
        foreach (var r in await Task.WhenAll(cleanups))
        {
            using (r)
            {
                if (!r.IsSuccessStatusCode)
                {
                    this.output.WriteLine(
                        $"TerminateInstance cleanup returned status {r.StatusCode} for request {r.RequestMessage?.RequestUri}");
                }
            }
        }
    }

    private async Task<(string instanceId, string statusUri)> StartOrchAndWaitForStatus(
        string orchestrationName, string targetStatus, DateTime? scheduledStartTime = null)
    {
        string functionName = "StartOrchestration";
        string queryParams = $"?orchestrationName={orchestrationName}";
        if (scheduledStartTime is not null)
        {
            functionName = "HelloCities_HttpStart_Scheduled";
            queryParams = $"?orchestrationName={orchestrationName}&ScheduledStartTime={scheduledStartTime:o}";
        }

        using var response = await HttpHelpers.InvokeHttpTrigger(functionName, queryParams);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        string statusUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusUri, targetStatus, 30);
        return (instanceId, statusUri);
    }

    private async Task TerminateAndWaitForStatus(string instanceId, string statusUri)
    {
        using var terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={instanceId}");
        Assert.Equal(HttpStatusCode.OK, terminateResponse.StatusCode);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusUri, "Terminated", 30);
    }

    private async Task SuspendAndWaitForStatus(string instanceId, string statusUri)
    {
        using var suspendResponse = await HttpHelpers.InvokeHttpTrigger("SuspendInstance", $"?instanceId={instanceId}");
        Assert.Equal(HttpStatusCode.OK, suspendResponse.StatusCode);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusUri, "Suspended", 30);
    }

    private async Task AssertPurgeSucceeds(string instanceId)
    {
        using var response = await HttpHelpers.InvokeHttpTrigger("PurgeOrchestrationHistory", $"?instanceId={instanceId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertPurgeCount(response, 1);
    }

    [Fact]
    [Trait("PowerShell", "Skip")] // Instance purging not supported in PowerShell
    [Trait("Java", "Skip")] // Entities are not implemented in Java
    [Trait("MSSQL", "Skip")] // Entities are not supported in MSSQL
    public async Task PurgeEntity()
    {
        // Start an orchestration that interacts with an entity
        HttpResponseMessage orchestrationResponse = await HttpHelpers.InvokeHttpTrigger(
            "StartOrchestration",
            "?orchestrationName=InvokeDummyEntityOrchestration");
        Assert.Equal(HttpStatusCode.Accepted, orchestrationResponse.StatusCode);

        // Wait for orchestration to complete
        await DurableHelpers.ParseInstanceIdAsync(orchestrationResponse);
        string orchestrationStatusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(orchestrationResponse);
        await DurableHelpers.WaitForOrchestrationStateAsync(orchestrationStatusQueryGetUri, "Completed", 30);

        string entityName = "DummyEntity";
        string entityKey = "myEntity";
        // Purge the entity instance
        using HttpResponseMessage purgeExistentEntity = await HttpHelpers.InvokeHttpTrigger(
            "PurgeOrchestrationHistory",
            $"?instanceId={new EntityInstanceId(entityName, entityKey)}");
        Assert.Equal(HttpStatusCode.OK, purgeExistentEntity.StatusCode);
        await AssertPurgeCount(purgeExistentEntity, 1);

        // Now attempt to purge a non-existent entity instance, purge count should be 0
        using HttpResponseMessage purgeNonExistentEntity = await HttpHelpers.InvokeHttpTrigger(
            "PurgeOrchestrationHistory",
            $"?instanceId={new EntityInstanceId(entityName + "3", entityKey)}");
        Assert.Equal(HttpStatusCode.OK, purgeNonExistentEntity.StatusCode);
        await AssertPurgeCount(purgeNonExistentEntity, 0);
    }

    private static async Task AssertPurgeCount(HttpResponseMessage purgeHttpResponse, int purgeCount)
    {
        string purgeMessage = await purgeHttpResponse.Content.ReadAsStringAsync();
        Assert.Matches($@"^Purged {purgeCount} records$", purgeMessage);
    }
}
