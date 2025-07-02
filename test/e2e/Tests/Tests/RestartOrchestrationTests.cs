// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Xunit;
using Xunit.Abstractions;
using System.Text.Json;

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
    // Test behavior of restartasync of durabletaskclient.
    // when restart with a instanceid and startwithnewinstanceid is false, the orchestration should be restarted with the same instance id.
    // and the output should be the same as the original orchestration.
    // when restart with a instanceid and startwithnewinstanceid is true, the orchestration should be restarted with a new instance id.
    // and the output should be same as the original orchestration.
    public async Task RestartOrchestration_CreatedTimeAndOutputChange(bool restartWithNewInstanceId)
    {
        // Start the orchestration
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("RestartOrchestration_HttpStart", "");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 10);
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

        await DurableHelpers.WaitForOrchestrationStateAsync(restartStatusQueryGetUri, "Completed", 10);
        var restartOrchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(restartStatusQueryGetUri);
        string output2 = restartOrchestrationDetails.Output;
        DateTime createdTime2 = restartOrchestrationDetails.CreatedTime;

        // The outputs and created times should be different
        Assert.Equal(output1, output2);
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
    }
} 