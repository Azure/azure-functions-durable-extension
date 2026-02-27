// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class RestartOrchestrationTests
{
    private readonly FunctionAppFixture fixture;
    private readonly ITestOutputHelper output;

    public RestartOrchestrationTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(testOutputHelper);
        this.output = testOutputHelper;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("PowerShell", "Skip")] // RestartAsync not yet implemented in PowerShell
    [Trait("Java", "Skip")] // RestartAsync not yet implemented in Java
    [Trait("Python", "Skip")] // RestartAsync not supported in Python
    [Trait("Node", "Skip")] // RestartAsync not supported in Node
    [Trait("MSSQL", "Skip")] // MSSQL doesn't support tags at ExecutionStarted Event.
    // Test behavior of restartasync of durabletaskclient.
    // When restart with a instanceid and startwithnewinstanceid is false, the orchestration should be restarted with the same instance id.
    // When restart with a instanceid and startwithnewinstanceid is true, the orchestration should be restarted with a new instance id.
    public async Task RestartOrchestration_CreatedTimeAndOutputChange(bool restartWithNewInstanceId)
    {
        // Start the orchestration
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("RestarttOrchestration_HttpStart/SimpleOrchestrator");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        // Verify that the ClientOperationReceived log was emitted with a FunctionInvocationId
        ClientOperationLogHelpers.AssertClientOperationLogExists(
            () => this.fixture.TestLogs.CoreToolsLogs,
            "StartOrchestration",
            instanceId,
            this.fixture.functionLanguageLocalizer.GetLanguageType());

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        string output1 = orchestrationDetails.Output;
        DateTime createdTime1 = orchestrationDetails.CreatedTime;

        // best practice to wait for 1 seconds before restarting orchestration to avoid race condition.
        await Task.Delay(1000);
        
        var restartPayload = new {
            InstanceId = instanceId,
            RestartWithNewInstanceId = restartWithNewInstanceId
        };

        string jsonBody = JsonSerializer.Serialize(restartPayload);
       
        // Restart the orchestrator with the same instance id)
        using HttpResponseMessage restartResponse = await HttpHelpers.InvokeHttpTriggerWithBody(
            "RestartOrchestration_HttpRestart", jsonBody, "application/json");
        Assert.Equal(HttpStatusCode.Accepted, restartResponse.StatusCode);
        string restartStatusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(restartResponse);
        string restartInstanceId = await DurableHelpers.ParseInstanceIdAsync(restartResponse);

        await DurableHelpers.WaitForOrchestrationStateAsync(restartStatusQueryGetUri, "Completed", 30);

        // Verify that the ClientOperationReceived log was emitted with a FunctionInvocationId
        ClientOperationLogHelpers.AssertClientOperationLogExists(
            () => this.fixture.TestLogs.CoreToolsLogs,
            "Restart",
            instanceId,
            this.fixture.functionLanguageLocalizer.GetLanguageType());

        var restartOrchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(restartStatusQueryGetUri);
        string output2 = restartOrchestrationDetails.Output;
        DateTime createdTime2 = restartOrchestrationDetails.CreatedTime;

        // The outputs should be the same as input is same.
        Assert.Equal(output1, output2);
        // Created time should be different.
        Assert.NotEqual(createdTime1, createdTime2);

        if (restartWithNewInstanceId)
        {
            // If restartWithNewInstanceId is True, the two instanceId should be different. 
            Assert.NotEqual(instanceId, restartInstanceId);
        }
        else
        {
            Assert.Equal(instanceId, restartInstanceId);
        }

        HttpResponseMessage result = await HttpHelpers.InvokeHttpTrigger("RestartOrchestrator_Query_Tags", $"?id={restartInstanceId}");

        // Verify that restarted orchestration instance contains tags.
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var content = await result.Content.ReadAsStringAsync();
        // Content: { "output": "...", "tags": { "testtag": "true" } }
        Assert.Contains("output", content);
        Assert.Contains("testtag", content);
    }

    [Fact]
    [Trait("PowerShell", "Skip")] // RestartAsync not yet implemented in PowerShell
    [Trait("Java", "Skip")] // RestartAsync not yet implemented in Java
    [Trait("Python", "Skip")] // RestartAsync not supported in Python
    [Trait("Node", "Skip")] // RestartAsync not supported in Node
    // Test that if we restart a instanceId that doesn't exist. We will throw ArgumentException exception.
    public async Task RestartOrchestration_NonExistentInstanceId_ShouldReturnNotFound()
    {
        const string testInstanceId = "nonexistid";
        
        // Test restarting with a non-existent instance ID
        var restartPayload = new
        {
            InstanceId = testInstanceId,
            RestartWithNewInstanceId = false
        };

        string jsonBody = JsonSerializer.Serialize(restartPayload);

        using HttpResponseMessage restartResponse = await HttpHelpers.InvokeHttpTriggerWithBody(
            "RestartOrchestration_HttpRestartWithErrorHandling", jsonBody, "application/json");
        
        string responseContent = await restartResponse.Content.ReadAsStringAsync();

        // Verfity we weill return the right exception message.
        Assert.Contains(fixture.functionLanguageLocalizer.GetLocalizedStringValue("RestartInvalidInstance.ErrorMessage", testInstanceId), responseContent);
    }

    [Fact]
    [Trait("PowerShell", "Skip")] // RestartAsync not yet implemented in PowerShell
    [Trait("Java", "Skip")] // RestartAsync not yet implemented in Java
    [Trait("Python", "Skip")] // RestartAsync not supported in Python
    [Trait("Node", "Skip")] // RestartAsync not supported in Node
    public async Task RestartOrchestration_NotCompletedOrchestrationWithRestartWithNewInstanceIdFalse_ShouldSucceed()
    {
        // Start a long-running orchestration
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("RestarttOrchestration_HttpStart/LongOrchestrator");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        DurableHelpers.OrchestrationStatusDetails orchestrationDetails
            = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        DateTime createdTime1 = orchestrationDetails.CreatedTime;

        // Wait for the orchestration to be running
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Running", 30);

        // Verify that the ClientOperationReceived log was emitted with a FunctionInvocationId
        ClientOperationLogHelpers.AssertClientOperationLogExists(
            () => this.fixture.TestLogs.CoreToolsLogs,
            "StartOrchestration",
            instanceId,
            this.fixture.functionLanguageLocalizer.GetLanguageType());

        // Try to restart the running orchestration with restartWithNewInstanceId = false
        var restartPayload = new
        {
            InstanceId = instanceId,
            RestartWithNewInstanceId = false
        };

        string jsonBody = JsonSerializer.Serialize(restartPayload);

        // Restart the orchestrator with the same instance id
        // This is necessary to ensure that the created times for the two orchestration instances are different.
        // The created time returned by the orchestration status API has a resolution only up to seconds, not milliseconds.
        Thread.Sleep(TimeSpan.FromSeconds(1));
        using HttpResponseMessage restartResponse = await HttpHelpers.InvokeHttpTriggerWithBody(
            "RestartOrchestration_HttpRestart", jsonBody, "application/json");
        Assert.Equal(HttpStatusCode.Accepted, restartResponse.StatusCode);

        string restartStatusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(restartResponse);
        string restartInstanceId = await DurableHelpers.ParseInstanceIdAsync(restartResponse);

        // Wait for the "created time" to change to verify that the orchestration was restarted
        DurableHelpers.OrchestrationStatusDetails restartOrchestrationDetails
            = await DurableHelpers.GetRunningOrchestrationDetailsAsync(restartStatusQueryGetUri);
        DateTime createdTime2 = restartOrchestrationDetails.CreatedTime;
        var waitForRestartTimeout = TimeSpan.FromSeconds(30);
        using CancellationTokenSource cts = new(waitForRestartTimeout);
        while (!cts.IsCancellationRequested && createdTime2 == createdTime1)
        {
            restartOrchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(restartStatusQueryGetUri);
            createdTime2 = restartOrchestrationDetails.CreatedTime;
        }
        Assert.NotEqual(createdTime1, createdTime2);
        Assert.Equal(instanceId, restartInstanceId);

        // Verify that the ClientOperationReceived log was emitted with a FunctionInvocationId
        ClientOperationLogHelpers.AssertClientOperationLogExists(
            () => this.fixture.TestLogs.CoreToolsLogs,
            "Restart",
            instanceId,
            this.fixture.functionLanguageLocalizer.GetLanguageType());

        // Clean up: terminate the long-running orchestration
        using HttpResponseMessage terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={instanceId}");
        Assert.Equal(HttpStatusCode.OK, terminateResponse.StatusCode);
    }
}
