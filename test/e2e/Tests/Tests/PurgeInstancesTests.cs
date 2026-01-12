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
        string purgeAgainMessage = await purgeAgainResponse.Content.ReadAsStringAsync();
        Assert.Matches(@"^Purged 0 records$", purgeAgainMessage);
        Assert.Equal(HttpStatusCode.OK, purgeAgainResponse.StatusCode);
    }

    [Fact]
    [Trait("PowerShell", "Skip")] // Instance purging not supported in PowerShell
    public async Task PurgeOnlyPurgesTerminalOrchestrations()
    {
        static async Task AssertPurgeNumber(HttpResponseMessage purgeHttpResponse)
        {
            string purgeMessage = await purgeHttpResponse.Content.ReadAsStringAsync();
            Assert.Matches(@"^Purged 1 records$", purgeMessage);
        }

        // For all of the following tests, since non-.NET languages throws a generic error in the case of a failure to purge there is no great way 
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

        // Completed orchestration, should succeed
        using HttpResponseMessage startCompletedOrchestrationResponse = await HttpHelpers.InvokeHttpTrigger(
            "StartOrchestration",
            "?orchestrationName=HelloCities");
        Assert.Equal(HttpStatusCode.Accepted, startCompletedOrchestrationResponse.StatusCode);
        string completedInstanceId = await DurableHelpers.ParseInstanceIdAsync(startCompletedOrchestrationResponse);
        string completedStatusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(startCompletedOrchestrationResponse);
        await DurableHelpers.WaitForOrchestrationStateAsync(completedStatusQueryGetUri, "Completed", 30);
        HttpResponseMessage purgeCompleted = await HttpHelpers.InvokeHttpTrigger("PurgeOrchestrationHistory", $"?instanceId={completedInstanceId}");
        Assert.Equal(HttpStatusCode.OK, purgeCompleted.StatusCode);
        await AssertPurgeNumber(purgeCompleted);

        // Terminated orchestration, should succeed
        using HttpResponseMessage startTerminatedOrchestrationResponse = await HttpHelpers.InvokeHttpTrigger(
            "StartOrchestration",
            "?orchestrationName=LongRunningOrchestrator");
        Assert.Equal(HttpStatusCode.Accepted, startTerminatedOrchestrationResponse.StatusCode);
        string terminatedInstanceId = await DurableHelpers.ParseInstanceIdAsync(startTerminatedOrchestrationResponse);
        string terminatedStatusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(startTerminatedOrchestrationResponse);
        await DurableHelpers.WaitForOrchestrationStateAsync(terminatedStatusQueryGetUri, "Running", 30);
        using HttpResponseMessage terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={terminatedInstanceId}");
        Assert.Equal(HttpStatusCode.OK, terminateResponse.StatusCode);
        await DurableHelpers.WaitForOrchestrationStateAsync(terminatedStatusQueryGetUri, "Terminated", 30);
        using HttpResponseMessage purgeTerminated = await HttpHelpers.InvokeHttpTrigger("PurgeOrchestrationHistory", $"?instanceId={terminatedInstanceId}");
        Assert.Equal(HttpStatusCode.OK, purgeTerminated.StatusCode);
        await AssertPurgeNumber(purgeTerminated);

        // Failed orchestration, should succeed
        using HttpResponseMessage startFailedOrchestrationResponse = await HttpHelpers.InvokeHttpTrigger(
            "StartOrchestration",
            "?orchestrationName=HelloActivityDIFailure");
        Assert.Equal(HttpStatusCode.Accepted, startFailedOrchestrationResponse.StatusCode);
        string failedInstanceId = await DurableHelpers.ParseInstanceIdAsync(startFailedOrchestrationResponse);
        string failedStatusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(startFailedOrchestrationResponse);
        await DurableHelpers.WaitForOrchestrationStateAsync(failedStatusQueryGetUri, "Failed", 30);
        using HttpResponseMessage purgeFailed = await HttpHelpers.InvokeHttpTrigger("PurgeOrchestrationHistory", $"?instanceId={failedInstanceId}");
        Assert.Equal(HttpStatusCode.OK, purgeFailed.StatusCode);
        await AssertPurgeNumber(purgeFailed);

        // Running orchestration, should fail
        using HttpResponseMessage startRunningOrchestrationResponse = await HttpHelpers.InvokeHttpTrigger(
            "StartOrchestration",
            "?orchestrationName=LongRunningOrchestrator");
        Assert.Equal(HttpStatusCode.Accepted, startRunningOrchestrationResponse.StatusCode);
        string runningInstanceId = await DurableHelpers.ParseInstanceIdAsync(startRunningOrchestrationResponse);
        string runningStatusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(startRunningOrchestrationResponse);
        await DurableHelpers.WaitForOrchestrationStateAsync(runningStatusQueryGetUri, "Running", 30);
        using HttpResponseMessage purgeRunning = await HttpHelpers.InvokeHttpTrigger("PurgeOrchestrationHistory", $"?instanceId={runningInstanceId}");
        AssertFailedPurgeResponseStatusCode(purgeRunning);

        // Pending orchestration, should fail
        DateTime scheduledStartTime = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        using HttpResponseMessage startPendingOrchestrationResponse = await HttpHelpers.InvokeHttpTrigger(
            "HelloCities_HttpStart_Scheduled",
            $"?ScheduledStartTime={scheduledStartTime:o}");
        Assert.Equal(HttpStatusCode.Accepted, startPendingOrchestrationResponse.StatusCode);
        string pendingInstanceId = await DurableHelpers.ParseInstanceIdAsync(startPendingOrchestrationResponse);
        string pendingStatusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(startPendingOrchestrationResponse);
        await DurableHelpers.WaitForOrchestrationStateAsync(pendingStatusQueryGetUri, "Pending", 30);
        using HttpResponseMessage purgePending = await HttpHelpers.InvokeHttpTrigger("PurgeOrchestrationHistory", $"?instanceId={pendingInstanceId}");
        AssertFailedPurgeResponseStatusCode(purgePending);

        // Suspended orchestration, should fail
        using HttpResponseMessage startSuspendedOrchestrationResponse = await HttpHelpers.InvokeHttpTrigger(
            "StartOrchestration",
            "?orchestrationName=LongRunningOrchestrator");
        Assert.Equal(HttpStatusCode.Accepted, startSuspendedOrchestrationResponse.StatusCode);
        string suspendedInstanceId = await DurableHelpers.ParseInstanceIdAsync(startSuspendedOrchestrationResponse);
        string suspendedStatusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(startSuspendedOrchestrationResponse);
        await DurableHelpers.WaitForOrchestrationStateAsync(suspendedStatusQueryGetUri, "Running", 30);
        using HttpResponseMessage suspendResponse = await HttpHelpers.InvokeHttpTrigger("SuspendInstance", $"?instanceId={suspendedInstanceId}");
        Assert.Equal(HttpStatusCode.OK, suspendResponse.StatusCode);
        await DurableHelpers.WaitForOrchestrationStateAsync(suspendedStatusQueryGetUri, "Suspended", 30);
        using HttpResponseMessage purgeSuspended = await HttpHelpers.InvokeHttpTrigger("PurgeOrchestrationHistory", $"?instanceId={suspendedInstanceId}");
        AssertFailedPurgeResponseStatusCode(purgeSuspended);

        // Entity, should succeed
        // MSSQL does not currently support entities, nor do PowerShell or Java
        if (this.fixture.GetDurabilityProvider() != FunctionAppFixture.ConfiguredDurabilityProviderType.MSSQL
            && this.fixture.functionLanguageLocalizer.GetLanguageType() != LanguageType.PowerShell
            && this.fixture.functionLanguageLocalizer.GetLanguageType() != LanguageType.Java)
        {
            // Start an orchestration that interacts with an entity
            HttpResponseMessage orchestrationResponse = await HttpHelpers.InvokeHttpTrigger(
                "StartOrchestration", 
                "?orchestrationName=CatchEntityOrchestration");
            Assert.Equal(HttpStatusCode.Accepted, orchestrationResponse.StatusCode);

            // Wait for orchestration to complete
            await DurableHelpers.ParseInstanceIdAsync(orchestrationResponse);
            string orchestrationStatusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(orchestrationResponse);
            await DurableHelpers.WaitForOrchestrationStateAsync(orchestrationStatusQueryGetUri, "Completed", 30);

            string entityName = "Counter";
            string entityKey = "myCounter";
            // Purge the entity instance
            using HttpResponseMessage purgeEntity = await HttpHelpers.InvokeHttpTrigger(
                "PurgeOrchestrationHistory",
                $"?instanceId={new EntityInstanceId(entityName, entityKey)}");
            Assert.Equal(HttpStatusCode.OK, purgeEntity.StatusCode);
            await AssertPurgeNumber(purgeEntity);
        }
    }
}
