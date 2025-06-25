// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class TerminateOrchestratorTests
{
    private readonly FunctionAppFixture fixture;
    private readonly ITestOutputHelper output;

    public TerminateOrchestratorTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(testOutputHelper);
        this.output = testOutputHelper;
    }


    [Fact]
    public async Task TerminateRunningOrchestration_ShouldSucceed()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("LongOrchestrator_HttpStart", "");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Running", 30);

        using HttpResponseMessage terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={instanceId}");
        await AssertTerminateRequestSucceedsAsync(terminateResponse);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Terminated", 30);
    }


    [Fact(Skip = "Will enable when https://github.com/Azure/azure-functions-durable-extension/issues/3025 is fixed")]
    public async Task TerminateScheduledOrchestration_ShouldSucceed()
    {
        DateTime scheduledStartTime = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("HelloCities_HttpStart_Scheduled", $"?scheduledStartTime={scheduledStartTime.ToString("o")}");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Pending", 30);

        using HttpResponseMessage terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={instanceId}");
        await AssertTerminateRequestSucceedsAsync(terminateResponse);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Terminated", 30);
    }


    [Fact]
    public async Task TerminateTerminatedOrchestration_ShouldFail()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("LongOrchestrator_HttpStart", "");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Running", 30);

        using HttpResponseMessage terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={instanceId}");
        await AssertTerminateRequestSucceedsAsync(terminateResponse);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Terminated", 30);

        using HttpResponseMessage terminateAgainResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={instanceId}");
        
        Assert.Equal(HttpStatusCode.BadRequest, terminateAgainResponse.StatusCode);

        // Check the exception returned contains the right statusCode and message. 
        string? terminateAgainResponseMessage = await terminateAgainResponse.Content.ReadAsStringAsync();
        Assert.NotNull(terminateAgainResponseMessage);

        Assert.Contains("StatusCode=\"FailedPrecondition\"", terminateAgainResponseMessage);
        Assert.Contains($"InvalidOperationException: Cannot terminate the orchestration instance {instanceId} because instance is in the Terminated state.", terminateAgainResponseMessage);

        // Give some time for Core Tools to write logs out
        Thread.Sleep(500);

        Assert.Contains(this.fixture.TestLogs.CoreToolsLogs, x => x.Contains("Cannot terminate orchestration instance in the Terminated state.") &&
                                                              x.Contains(instanceId));
    }


    [Fact]
    public async Task TerminateCompletedOrchestration_ShouldFail()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("HelloCities_HttpStart", "");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        using HttpResponseMessage terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={instanceId}");
        
        Assert.Equal(HttpStatusCode.BadRequest, terminateResponse.StatusCode);

        string? terminateResponseMessage = await terminateResponse.Content.ReadAsStringAsync();
        Assert.NotNull(terminateResponseMessage);

        // Check the exception returned contains the right statusCode and message. 
        Assert.Contains("StatusCode=\"FailedPrecondition\"", terminateResponseMessage);
        Assert.Contains($"InvalidOperationException: Cannot terminate the orchestration instance {instanceId} because instance is in the Completed state.", terminateResponseMessage);

        // Give some time for Core Tools to write logs out
        Thread.Sleep(500);

        Assert.Contains(this.fixture.TestLogs.CoreToolsLogs, x => x.Contains("Cannot terminate orchestration instance in the Completed state.") &&
                                                              x.Contains(instanceId));
    }

    [Fact]
    public async Task TerminateNonExistantOrchestration_ShouldFail()
    {
        string instanceId = Guid.NewGuid().ToString();
        using HttpResponseMessage terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={instanceId}");
        Assert.Equal(HttpStatusCode.BadRequest, terminateResponse.StatusCode);

        string? terminateResponseMessage = await terminateResponse.Content.ReadAsStringAsync();
        Assert.NotNull(terminateResponseMessage);

        // Check the exception returned contains the right statusCode and message. 
        Assert.Contains("Status(StatusCode=\"NotFound\"", terminateResponseMessage);
        Assert.Contains($"ArgumentException: No instance with ID '{instanceId}' was found.", terminateResponseMessage);
    }

    private static async Task AssertTerminateRequestSucceedsAsync(HttpResponseMessage terminateResponse)
    {
        Assert.Equal(HttpStatusCode.OK, terminateResponse.StatusCode);

        string? terminateResponseMessage = await terminateResponse.Content.ReadAsStringAsync();
        Assert.NotNull(terminateResponseMessage);
        Assert.Empty(terminateResponseMessage);
    }
}
