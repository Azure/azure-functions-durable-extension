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
    public async Task CanStartOrchestration_WithSameId_ForAllStatuses_ForEmptyDedupeStatuses()
    {
        HttpResponseMessage terminateResponse;

        // Completed
        string completedInstanceId = Guid.NewGuid().ToString();
        using HttpResponseMessage startCompletedResponseFirstAttempt = await StartAndWaitForState(
            "HelloCities", completedInstanceId, "Completed");
        using HttpResponseMessage startCompletedResponseSecondAttempt = await StartAndWaitForState(
            "HelloCities", completedInstanceId, "Completed");

        // Failed
        // This invocation will fail because the "LargeOutputOrchestrator" expects a non-zero input, but we provide none
        string failedInstanceId = Guid.NewGuid().ToString();
        using HttpResponseMessage startFailedResponseFirstAttempt = await StartAndWaitForState(
            "LargeOutputOrchestrator", failedInstanceId, "Failed");
        using HttpResponseMessage startFailedResponseSecondAttempt = await StartAndWaitForState(
            "LargeOutputOrchestrator", failedInstanceId, "Failed");

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
            // Clean-up
            terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={terminatedInstanceId}");
            Assert.Equal(HttpStatusCode.OK, terminateResponse.StatusCode);
            terminateResponse.Dispose();
        }

        // Pending
        // Scheduled start times are currently only implemented in Java and .NET isolated, which is the only true way
        // to get an orchestration in a "Pending" state
        if (this.fixture.functionLanguageLocalizer.GetLanguageType() == LanguageType.DotnetIsolated
            || this.fixture.functionLanguageLocalizer.GetLanguageType() == LanguageType.Java)
        {
            string pendingInstanceId = Guid.NewGuid().ToString();
            DateTime scheduledStartTime = DateTime.UtcNow.AddMinutes(10);
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
        // Clean-up
        terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={runningInstanceId}");
        Assert.Equal(HttpStatusCode.OK, terminateResponse.StatusCode);
        terminateResponse.Dispose();

        // Suspended
        // Bug: https://github.com/microsoft/durabletask-mssql/issues/300
        // Since it is not possible to terminate a suspended orchestration in MSSQL, the start orchestration request 
        // will timeout waiting for the existing orchestration to terminate before creating the new one
        if (this.fixture.GetDurabilityProvider() != FunctionAppFixture.ConfiguredDurabilityProviderType.MSSQL)
        {
            string suspendedInstanceId = Guid.NewGuid().ToString();
            using HttpResponseMessage startSuspendedResponseFirstAttempt = await StartAndWaitForState(
                "LongRunningOrchestrator", suspendedInstanceId, "Running");
            await SuspendAndWaitForState(suspendedInstanceId, startSuspendedResponseFirstAttempt);
            using HttpResponseMessage startSuspendedResponseSecondAttempt = await StartAndWaitForState(
                "LongRunningOrchestrator", suspendedInstanceId, "Running");
            // Clean-up
            terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={suspendedInstanceId}");
            Assert.Equal(HttpStatusCode.OK, terminateResponse.StatusCode);
            terminateResponse.Dispose();
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
        HttpResponseMessage terminateResponse;

        // Completed
        string completedInstanceId = Guid.NewGuid().ToString();
        using HttpResponseMessage startCompletedResponseFirstAttempt = await StartAndWaitForStateWithDedupeStatuses(
            "HelloCities", completedInstanceId, "Completed", dedupeStatuses);
        using HttpResponseMessage startCompletedResponseSecondAttempt = await StartAndWaitForStateWithDedupeStatuses(
            "HelloCities",
            completedInstanceId,
            "Completed",
            dedupeStatuses,
            expectedCode: dedupeStatuses.Contains("Completed") ? HttpStatusCode.Conflict : HttpStatusCode.Accepted);

        // Terminated
        string terminatedInstanceId = Guid.NewGuid().ToString();
        using HttpResponseMessage startTerminatedResponseFirstAttempt = await StartAndWaitForStateWithDedupeStatuses(
            "LongRunningOrchestrator", terminatedInstanceId, "Running", dedupeStatuses);
        await TerminateAndWaitForState(terminatedInstanceId, startTerminatedResponseFirstAttempt);
        using HttpResponseMessage startTerminatedResponseSecondAttempt = await StartAndWaitForStateWithDedupeStatuses(
            "LongRunningOrchestrator",
            terminatedInstanceId,
            "Running",
            dedupeStatuses,
            expectedCode: dedupeStatuses.Contains("Terminated") ? HttpStatusCode.Conflict : HttpStatusCode.Accepted);
        // Clean-up
        if (!dedupeStatuses.Contains("Terminated"))
        {
            terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={terminatedInstanceId}");
            Assert.Equal(HttpStatusCode.OK, terminateResponse.StatusCode);
            terminateResponse.Dispose();
        }

        // Failed
        // This invocation will fail because the "LargeOutputOrchestrator" expects a non-zero input, but we provide none
        string failedInstanceId = Guid.NewGuid().ToString();
        using HttpResponseMessage startFailedResponseFirstAttempt = await StartAndWaitForStateWithDedupeStatuses(
            "LargeOutputOrchestrator", failedInstanceId, "Failed", dedupeStatuses);
        using HttpResponseMessage startFailedResponseSecondAttempt = await StartAndWaitForStateWithDedupeStatuses(
            "LargeOutputOrchestrator",
            failedInstanceId,
            "Failed",
            dedupeStatuses,
            expectedCode: dedupeStatuses.Contains("Failed") ? HttpStatusCode.Conflict : HttpStatusCode.Accepted);

        // Pending
        string pendingInstanceId = Guid.NewGuid().ToString();
        DateTime scheduledStartTime = DateTime.UtcNow.AddMinutes(2);
        using HttpResponseMessage startPendingResponseFirstAttempt = await StartAndWaitForStateWithDedupeStatuses(
            "HelloCities", pendingInstanceId, "Pending", dedupeStatuses, scheduledStartTime: scheduledStartTime);
        using HttpResponseMessage startPendingResponseSecondAttempt = await StartAndWaitForStateWithDedupeStatuses(
            "HelloCities",
            pendingInstanceId,
            "Completed",
            dedupeStatuses,
            expectedCode: dedupeStatuses.Contains("Pending") ? HttpStatusCode.Conflict : HttpStatusCode.Accepted);

        // Running
        string runningInstanceId = Guid.NewGuid().ToString();
        using HttpResponseMessage startRunningResponseFirstAttempt = await StartAndWaitForStateWithDedupeStatuses(
            "LongRunningOrchestrator", runningInstanceId, "Running", dedupeStatuses);
        using HttpResponseMessage startRunningResponseSecondAttempt = await StartAndWaitForStateWithDedupeStatuses(
            "LongRunningOrchestrator",
            runningInstanceId,
            expectedState: "Running",
            dedupeStatuses,
            expectedCode: dedupeStatuses.Contains("Running") ? HttpStatusCode.Conflict : HttpStatusCode.Accepted);
        // Clean-up
        terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={runningInstanceId}");
        Assert.Equal(HttpStatusCode.OK, terminateResponse.StatusCode);
        terminateResponse.Dispose();

        // Suspended
        // Bug: https://github.com/microsoft/durabletask-mssql/issues/300
        // Since it is not possible to terminate a suspended orchestration in MSSQL, the start orchestration request 
        // will timeout waiting for the existing orchestration to terminate before creating the new one
        if (this.fixture.GetDurabilityProvider() != FunctionAppFixture.ConfiguredDurabilityProviderType.MSSQL)
        {
            string suspendedInstanceId = Guid.NewGuid().ToString();
            using HttpResponseMessage startSuspendedResponseFirstAttempt = await StartAndWaitForStateWithDedupeStatuses(
                "LongRunningOrchestrator", suspendedInstanceId, "Running", dedupeStatuses);
            await SuspendAndWaitForState(suspendedInstanceId, startSuspendedResponseFirstAttempt);
            using HttpResponseMessage startSuspendedResponseSecondAttempt = await StartAndWaitForStateWithDedupeStatuses(
                "LongRunningOrchestrator",
                suspendedInstanceId,
                "Running",
                dedupeStatuses,
                expectedCode: dedupeStatuses.Contains("Suspended") ? HttpStatusCode.Conflict : HttpStatusCode.Accepted);
            // Clean-up
            terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={suspendedInstanceId}");
            Assert.Equal(HttpStatusCode.OK, terminateResponse.StatusCode);
            terminateResponse.Dispose();
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
            queryString += $"&scheduledStartTime={scheduledStartTime:o}";
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
