using System.Net;
using System.Text.Json;
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
    public async Task CanStartOrchestrationWithSameIdForAllStatusesForEmptyDedupeStatuses()
    {
        // Completed
        string completedInstanceId = Guid.NewGuid().ToString();
        using HttpResponseMessage startCompletedResponseFirstAttempt = await StartAndWaitForState(
            "HelloCities", completedInstanceId, "Completed");
        using HttpResponseMessage startCompletedResponseSecondAttempt = await StartAndWaitForState(
            "HelloCities", completedInstanceId, "Completed");

        // Failed
        string failedInstanceId = Guid.NewGuid().ToString();
        using HttpResponseMessage startFailedResponseFirstAttempt = await StartAndWaitForState(
            "RethrowActivityException", failedInstanceId, "Failed");
        // Invoking this same orchestration with the same instance ID will cause it to complete successfully on the second attempt,
        // hence we look for a "Completed" status instead
        using HttpResponseMessage startFailedResponseSecondAttempt = await StartAndWaitForState(
            "RethrowActivityException", failedInstanceId, "Completed");

        // Terminated
        if (this.fixture.functionLanguageLocalizer.GetLanguageType() != LanguageType.Java
            || this.fixture.GetDurabilityProvider() != FunctionAppFixture.ConfiguredDurabilityProviderType.MSSQL) // Bug: https://github.com/microsoft/durabletask-java/issues/237
        {
            string terminatedInstanceId = Guid.NewGuid().ToString();
            using HttpResponseMessage startTerminatedResponseFirstAttempt = await StartAndWaitForState(
                "LongRunningOrchestrator", terminatedInstanceId, "Running");
            await TerminateAndWaitForState(terminatedInstanceId, startTerminatedResponseFirstAttempt);
            using HttpResponseMessage startTerminatedResponseSecondAttempt = await StartAndWaitForState(
                "LongRunningOrchestrator", terminatedInstanceId, "Running");
        }

        // Pending
        // Scheduled start times are currently only implemented in Java and .NET isolated, which is the only true way
        // to get an orchestration in a "Pending" state
        if (this.fixture.functionLanguageLocalizer.GetLanguageType() == LanguageType.DotnetIsolated
            || this.fixture.functionLanguageLocalizer.GetLanguageType() == LanguageType.Java)
        {
            string pendingInstanceId = Guid.NewGuid().ToString();
            DateTime scheduledStartTime = DateTime.UtcNow.AddMinutes(2);
            using HttpResponseMessage startPendingResponseFirstAttempt = await StartAndWaitForState(
                "HelloCities", pendingInstanceId, "Pending", scheduledStartTime: scheduledStartTime);
            using HttpResponseMessage startPendingResponseSecondAttempt = await StartAndWaitForState(
                "HelloCities", pendingInstanceId, "Completed");
        }

        // Running
        string runningInstanceId = Guid.NewGuid().ToString();
        using HttpResponseMessage startRunningResponseFirstAttempt = await StartAndWaitForState(
            "LongRunningOrchestrator", runningInstanceId, "Running");
        using HttpResponseMessage startRunningResponseSecondAttempt = await StartAndWaitForState(
            "LongRunningOrchestrator", runningInstanceId, "Running");

        // Suspended
        string suspendedInstanceId = Guid.NewGuid().ToString();
        using HttpResponseMessage startSuspendedResponseFirstAttempt = await StartAndWaitForState(
            "LongRunningOrchestrator", suspendedInstanceId, "Running");
        await SuspendAndWaitForState(suspendedInstanceId, startSuspendedResponseFirstAttempt);
        using HttpResponseMessage startSuspendedResponseSecondAttempt = await StartAndWaitForState(
            "LongRunningOrchestrator", suspendedInstanceId, "Running");
    }

    [Fact]
    [Trait("PowerShell", "Skip")] // Dedupe statuses not implemented in PowerShell
    [Trait("Python", "Skip")] // Dedupe statuses not implemented in Python
    [Trait("Node", "Skip")] // Dedupe statuses not implemented in Node
    [Trait("Java", "Skip")] // Dedupe statuses not implemented in Java
    public async Task StartOrchestrationWithSameIdFailsForDedupeStatuses()
    {
        List<string> dedupeStatuses = ["Running", "Failed"];

        // Completed, should succeed
        string completedInstanceId = Guid.NewGuid().ToString();
        using HttpResponseMessage startCompletedResponseFirstAttempt = await StartAndWaitForStateWithDedupeStatuses(
            "HelloCities", completedInstanceId, "Completed", dedupeStatuses);
        using HttpResponseMessage startCompletedResponseSecondAttempt = await StartAndWaitForStateWithDedupeStatuses(
            "HelloCities", completedInstanceId, "Completed", dedupeStatuses);

        // Terminated
        string terminatedInstanceId = Guid.NewGuid().ToString();
        using HttpResponseMessage startTerminatedResponseFirstAttempt = await StartAndWaitForStateWithDedupeStatuses(
            "LongRunningOrchestrator", terminatedInstanceId, "Running", dedupeStatuses);
        await TerminateAndWaitForState(terminatedInstanceId, startTerminatedResponseFirstAttempt);
        using HttpResponseMessage startTerminatedResponseSecondAttempt = await StartAndWaitForStateWithDedupeStatuses(
            "LongRunningOrchestrator", terminatedInstanceId, "Running", dedupeStatuses);

        // Pending
        string pendingInstanceId = Guid.NewGuid().ToString();
        DateTime scheduledStartTime = DateTime.UtcNow.AddMinutes(2);
        using HttpResponseMessage startPendingResponseFirstAttempt = await StartAndWaitForStateWithDedupeStatuses(
            "HelloCities", pendingInstanceId, "Pending", dedupeStatuses, scheduledStartTime: scheduledStartTime);
        using HttpResponseMessage startPendingResponseSecondAttempt = await StartAndWaitForStateWithDedupeStatuses(
            "HelloCities", pendingInstanceId, "Completed", dedupeStatuses);

        // Suspended
        string suspendedInstanceId = Guid.NewGuid().ToString();
        using HttpResponseMessage startSuspendedResponseFirstAttempt = await StartAndWaitForStateWithDedupeStatuses(
            "LongRunningOrchestrator", suspendedInstanceId, "Running", dedupeStatuses);
        await SuspendAndWaitForState(suspendedInstanceId, startSuspendedResponseFirstAttempt);
        using HttpResponseMessage startSuspendedResponseSecondAttempt = await StartAndWaitForStateWithDedupeStatuses(
            "LongRunningOrchestrator", suspendedInstanceId, "Running", dedupeStatuses);

        // Failed, should fail
        string failedInstanceId = Guid.NewGuid().ToString();
        using HttpResponseMessage startFailedResponseFirstAttempt = await StartAndWaitForStateWithDedupeStatuses(
            "RethrowActivityException", failedInstanceId, "Failed", dedupeStatuses);
        // We do not provide an expected state since we expect the request to fail
        using HttpResponseMessage startFailedResponseSecondAttempt = await StartAndWaitForStateWithDedupeStatuses(
            "RethrowActivityException", failedInstanceId, expectedState: string.Empty, dedupeStatuses, expectedCode: HttpStatusCode.Conflict);

        // Running, should fail
        string runningInstanceId = Guid.NewGuid().ToString();
        using HttpResponseMessage startRunningResponseFirstAttempt = await StartAndWaitForStateWithDedupeStatuses(
            "LongRunningOrchestrator", runningInstanceId, "Running", dedupeStatuses);
        // We do not provide an expected state since we expect the request to fail
        using HttpResponseMessage startRunningResponseSecondAttempt = await StartAndWaitForStateWithDedupeStatuses(
            "LongRunningOrchestrator", runningInstanceId, expectedState: string.Empty, dedupeStatuses, expectedCode: HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("PowerShell", "Skip")] // Dedupe statuses not implemented in PowerShell
    [Trait("Python", "Skip")] // Dedupe statuses not implemented in Python
    [Trait("Node", "Skip")] // Dedupe statuses not implemented in Node
    [Trait("Java", "Skip")] // Dedupe statuses not implemented in Java
    public async Task StartOrchestrationWithInvalidDedupeStatusesFails()
    {
        // Dedupe statuses cannot have both "Terminated" and a running status (in this case "Pending")
        List<string> dedupeStatuses = ["Pending", "Failed", "Terminated"];

        // We do not provide an expected state since we expect the request to fail
        using HttpResponseMessage failedRequest = await StartAndWaitForStateWithDedupeStatuses(
            "HelloCities", Guid.NewGuid().ToString(), expectedState: string.Empty, dedupeStatuses, expectedCode: HttpStatusCode.BadRequest);
    }

    private static async Task<HttpResponseMessage> StartAndWaitForState(
        string orchestrationName,
        string instanceId,
        string expectedState,
        DateTime? scheduledStartTime = null)
    {
        string functionName = "StartOrchestration";
        string queryString = $"?orchestrationName={orchestrationName}&instanceId={instanceId}";

        if (scheduledStartTime is not null)
        {
            queryString += $"&scheduledStartTime={scheduledStartTime:o}";
            functionName = "HelloCities_HttpStart_Scheduled";
        }

        HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(functionName, queryString);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, expectedState, 60);
        return response;
    }

    private static async Task<HttpResponseMessage> StartAndWaitForStateWithDedupeStatuses(
        string orchestrationName,
        string instanceId,
        string expectedState,
        List<string> dedupeStatuses,
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
        return response;
    }

    private static async Task TerminateAndWaitForState(string instanceId, HttpResponseMessage startOrchestrationResponse)
    {
        using HttpResponseMessage terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={instanceId}");
        Assert.Equal(HttpStatusCode.OK, terminateResponse.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(startOrchestrationResponse);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Terminated", 60);
    }

    private static async Task SuspendAndWaitForState(string instanceId, HttpResponseMessage startOrchestrationResponse)
    {
        using HttpResponseMessage suspendResponse = await HttpHelpers.InvokeHttpTrigger("SuspendInstance", $"?instanceId={instanceId}");
        Assert.Equal(HttpStatusCode.OK, suspendResponse.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(startOrchestrationResponse);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Suspended", 60);
    }
}
