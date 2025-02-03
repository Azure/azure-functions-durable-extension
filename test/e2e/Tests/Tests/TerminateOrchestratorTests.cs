// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class TerminateOrchestratorTests
{
    private readonly FunctionAppFixture _fixture;
    private readonly ITestOutputHelper _output;

    public TerminateOrchestratorTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _fixture.TestLogs.UseTestLogger(testOutputHelper);
        _output = testOutputHelper;
    }

    private static async Task AssertTerminateRequestFails(HttpResponseMessage terminateResponse)
    {
        Assert.Equal(HttpStatusCode.BadRequest, terminateResponse.StatusCode);

        string? terminateResponseMessage = await terminateResponse.Content.ReadAsStringAsync();
        Assert.NotNull(terminateResponseMessage);
        Assert.Equal("Status(StatusCode=\"Unknown\", Detail=\"Exception was thrown by handler.\")", terminateResponseMessage);
    }

    private static async Task AssertTerminateRequestSucceeds(HttpResponseMessage terminateResponse)
    {
        Assert.Equal(HttpStatusCode.OK, terminateResponse.StatusCode);

        string? terminateResponseMessage = await terminateResponse.Content.ReadAsStringAsync();
        Assert.NotNull(terminateResponseMessage);
        Assert.Empty(terminateResponseMessage);
    }


    [Fact]
    public async Task TerminateRunningOrchestration_ShouldSucceed()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("LongOrchestrator_HttpStart", "");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceId(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUri(response);

        Thread.Sleep(1000);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetails(statusQueryGetUri);
        Assert.Equal("Running", orchestrationDetails.RuntimeStatus);

        using HttpResponseMessage terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={instanceId}");
        await AssertTerminateRequestSucceeds(terminateResponse);

        Thread.Sleep(1000);

        orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetails(statusQueryGetUri);
        Assert.Equal("Terminated", orchestrationDetails.RuntimeStatus);
    }


    // This test is likely exposing some unintended behavior. Currently, attempting to terminate scheduled orchestrations has no effect.
    // If the behavior of Terminate is changed to accomodate terminating scheduled orchestrations, this test will need to be modified accordingly.
    [Fact]
    public async Task TerminateScheduledOrchestration_ShouldDoNothing()
    {
        DateTime scheduledStartTime = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("HelloCities_HttpStart_Scheduled", $"?scheduledStartTime={scheduledStartTime.ToString("o")}");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceId(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUri(response);

        Thread.Sleep(1000);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetails(statusQueryGetUri);
        Assert.Equal("Pending", orchestrationDetails.RuntimeStatus);

        using HttpResponseMessage terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={instanceId}");
        Assert.Equal(HttpStatusCode.OK, terminateResponse.StatusCode);

        string? terminateResponseMessage = await terminateResponse.Content.ReadAsStringAsync();
        Assert.NotNull(terminateResponseMessage);
        Assert.Empty(terminateResponseMessage);

        Thread.Sleep(1000);

        orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetails(statusQueryGetUri);
        Assert.Equal("Pending", orchestrationDetails.RuntimeStatus);
    }


    [Fact]
    public async Task TerminateTerminatedOrchestration_ShouldFail()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("LongOrchestrator_HttpStart", "");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceId(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUri(response);

        Thread.Sleep(1000);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetails(statusQueryGetUri);
        Assert.Equal("Running", orchestrationDetails.RuntimeStatus);

        using HttpResponseMessage terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={instanceId}");
        await AssertTerminateRequestSucceeds(terminateResponse);

        Thread.Sleep(1000);
        using HttpResponseMessage terminateAgainResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={instanceId}");
        await AssertTerminateRequestFails(terminateAgainResponse);

        // Give some time for Core Tools to write logs out
        Thread.Sleep(500);

        Assert.Contains(_fixture.TestLogs.CoreToolsLogs, x => x.Contains("Cannot terminate orchestration instance in the Terminated state.") &&
                                                              x.Contains(instanceId));

        orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetails(statusQueryGetUri);
        Assert.Equal("Terminated", orchestrationDetails.RuntimeStatus);
    }


    [Fact]
    public async Task TerminateCompletedOrchestration_ShouldFail()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("HelloCities_HttpStart", "");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceId(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUri(response);

        Thread.Sleep(1000);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetails(statusQueryGetUri);
        Assert.Equal("Completed", orchestrationDetails.RuntimeStatus);

        using HttpResponseMessage terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={instanceId}");
        await AssertTerminateRequestFails(terminateResponse);

        // Give some time for Core Tools to write logs out
        Thread.Sleep(500);

        Assert.Contains(_fixture.TestLogs.CoreToolsLogs, x => x.Contains("Cannot terminate orchestration instance in the Completed state.") &&
                                                              x.Contains(instanceId));
    }

    [Fact]
    public async Task TerminateNonExistantOrchestration_ShouldFail()
    {
        using HttpResponseMessage terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={Guid.NewGuid().ToString()}");
        await AssertTerminateRequestFails(terminateResponse);
    }
}
