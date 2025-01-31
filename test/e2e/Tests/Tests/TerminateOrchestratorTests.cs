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

    // Due to some kind of asynchronous race condition in XUnit, when running these tests in pipelines,
    // the output may be disposed before the message is written. Just ignore these types of errors for now. 
    private void WriteOutput(string message)
    {
        try
        {
            _output.WriteLine(message);
        }
        catch
        {
            // Ignore
        }
    }

    [Theory]
    [InlineData("LongOrchestrator_HttpStart", HttpStatusCode.Accepted)]
    public async Task HttpTriggerTests(string functionName, HttpStatusCode expectedStatusCode)
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(functionName, "");
        string actualMessage = await response.Content.ReadAsStringAsync();

        Assert.Equal(expectedStatusCode, response.StatusCode);
        string instanceId = DurableHelpers.ParseInstanceId(response);
        string statusQueryGetUri = DurableHelpers.ParseStatusQueryGetUri(response);

        Thread.Sleep(1000);

        var orchestrationDetails = DurableHelpers.GetRunningOrchestrationDetails(statusQueryGetUri);
        Assert.Equal("Running", orchestrationDetails.RuntimeStatus);

        using HttpResponseMessage terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={instanceId}");
        Assert.Equal(HttpStatusCode.OK, terminateResponse.StatusCode);

        string? terminateResponseMessage = terminateResponse.Content?.ReadAsStringAsync().Result;
        Assert.NotNull(terminateResponseMessage);
        Assert.Empty(terminateResponseMessage);

        Thread.Sleep(1000);

        orchestrationDetails = DurableHelpers.GetRunningOrchestrationDetails(statusQueryGetUri);
        Assert.Equal("Terminated", orchestrationDetails.RuntimeStatus);
    }
}
