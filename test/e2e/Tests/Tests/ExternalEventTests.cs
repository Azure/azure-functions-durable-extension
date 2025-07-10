// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class ExternalEventTests
{
    private readonly FunctionAppFixture fixture;
    private readonly ITestOutputHelper output;

    public ExternalEventTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(testOutputHelper);
        this.output = testOutputHelper;
    }

    // Test that sending an event to a running orchestrator waiting for an external event will complete successfully,
    // and sending an event to a completed instance will throw a FailedPrecondition RpcException with details error message.
    [Fact]
    public async Task RaiseExternalEventTests()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=ExternalEventOrchestrator");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        string jsonContent = JsonSerializer.Serialize(instanceId);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        // Send Event to the above Orchestrator which is waiting for external event.
        await HttpHelpers.InvokeHttpTriggerWithBody("SendExternalEvent_HttpStart", jsonContent, "application/json");

        // Make sure orchestration instance completes successfully.
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        // Send external event again to the completed orchestrator, which we will get a exception back.
        HttpResponseMessage resendEventResponse = await HttpHelpers.InvokeHttpTriggerWithBody("SendExternalEvent_HttpStart", jsonContent, "application/json");
        string responseContent = await resendEventResponse.Content.ReadAsStringAsync();

        // Verify the returned exception contains the correct information. 
        // In dotnet-isolated, this is the StatusCode of the RPC exception. 
        // In other languages, it is the exception type
        Assert.Contains(fixture.functionLanguageLocalizer.GetLocalizedStringValue("ExternalEvent.CompletedInstance.ErrorName"), responseContent);

        // In dotnet-isolated, this is the deliberate error text from the RpcException
        // In other languages, it is the symptom error
        Assert.Contains(fixture.functionLanguageLocalizer.GetLocalizedStringValue("ExternalEvent.CompletedInstance.ErrorMessage"), responseContent);
    }

    // Test that sending an event to a not-exist InstanceId will throw an NotFoundRpc Exception.
    [Fact]
    public async Task NotFoundInstanceTest()
    {
        string jsonContent = JsonSerializer.Serialize("instance-does-not-exist-test");
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTriggerWithBody("SendExternalEvent_HttpStart", jsonContent, "application/json");
        string responseContent = await response.Content.ReadAsStringAsync();

        // Verify the returned exception contains the correct information. 
        // In dotnet-isolated, this is the StatusCode of the RPC exception. 
        // In other languages, it is the exception type
        Assert.Contains(fixture.functionLanguageLocalizer.GetLocalizedStringValue("ExternalEvent.InvalidInstance.ErrorName"), responseContent);

        // In dotnet-isolated, this is the deliberate error text from the RpcException
        // In other languages, it is the symptom error
        Assert.Contains(fixture.functionLanguageLocalizer.GetLocalizedStringValue("ExternalEvent.InvalidInstance.ErrorMessage"), responseContent);
    }
}
