// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class ExternalEventTests
{
    private readonly FunctionAppFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ExternalEventTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _fixture.TestLogs.UseTestLogger(testOutputHelper);
        _output = testOutputHelper;
    }

    // Test that sending an event to a running orchestrator waiting for an external event will complete successfully,
    // and sending an event to a completed instance will throw a FailedPrecondition RpcException with details error message.
    [Fact]
    public async Task RaiseExternalEventTests()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("ExternalEventOrchestrator_HttpStart", "");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        // Send Event to the above Orchestrator which is waiting for external event.
        await HttpHelpers.InvokeHttpTrigger("SendExternalEvent_HttpStart", "");

        // Make sure orchestration instance completes successfully.
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        // Send external event again to the completed orchestrator, which we will get a exception back.
        HttpResponseMessage resendEventResponse = await HttpHelpers.InvokeHttpTrigger("SendExternalEvent_HttpStart", "");
        string responseContent = await resendEventResponse.Content.ReadAsStringAsync();

        // Verify the returned exception contains the correct information. 
        Assert.Contains("FailedPrecondition", responseContent);
        Assert.Contains("The orchestration instance with the provided instance id is not running.", responseContent);
    }

    // Test that sending an event with an empty instance ID throws an ArgumentException.
    [Fact]
    public async Task NotValidInstanceTest()
    {
        // Send Event to a empty string Instance Id and an ArgumentException will return.
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("NotValidInstance_HttpStart", "");
        string responseContent = await response.Content.ReadAsStringAsync();

        // Verify the returned exception contains the correct information. 
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("ArgumentException", responseContent);
        Assert.Contains("instanceId", responseContent);
    }
}
