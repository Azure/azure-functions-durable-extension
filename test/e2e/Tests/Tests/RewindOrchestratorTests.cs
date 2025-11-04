// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class RewindOrchestratorTests
{
    private readonly FunctionAppFixture fixture;
    private readonly ITestOutputHelper output;

    public RewindOrchestratorTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(testOutputHelper);
        this.output = testOutputHelper;
    }

    [Theory]
    [Trait("DTS", "Skip")] // Need to wait for the emulator to be released with the new rewind implementation
    [Trait("MSSQL", "Skip")] // Rewind is not implemented in the MSSQL backend
    [InlineData(1)]
    [InlineData(2)]
    public async Task RewindFailedOrchestration_ShouldSucceed(int numFailures)
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(
            "HttpStart_RewindOrchestration",
            $"?orchestrationName=RewindParentOrchestration&input=fail&numFailures={numFailures}");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        for (int i = 0; i < numFailures; i++)
        {
            await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Failed", 30);

            using HttpResponseMessage rewindResponse = await HttpHelpers.InvokeHttpTrigger("RewindInstance", $"?instanceId={instanceId}");
            Assert.Equal(HttpStatusCode.OK, rewindResponse.StatusCode);
        }
            
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);
        DurableHelpers.OrchestrationStatusDetails orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        Dictionary<string, int>? output = JsonSerializer.Deserialize<Dictionary<string, int>>(orchestrationDetails.Output);
        Assert.NotNull(output);

        // Confirm that each of the successful Activities/entities were invoked only once, while the failed Activities were invoked upon the first attempt
        // and for each successive rewind as well (so numFailures + 1) times).
        foreach (KeyValuePair<string, int> kvp in output)
        {
            if (kvp.Key.Contains("fail_activity"))
            {
                Assert.Equal(1 + numFailures, kvp.Value);
            }
            else
            {
                Assert.Equal(1, kvp.Value);
            }
        }
    }

    [Fact]
    public async Task RewindOnlyRewindsFailedOrchestrations()
    {
        // Try to rewind a completed, running, terminated, and pending orchestration - all should fail
        HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(
            "HttpStart_RewindOrchestration",
            $"?orchestrationName=RewindParentOrchestration&input=complete&numFailures=0");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        // Rewind a completed orchestration
        HttpResponseMessage rewindResponse = await HttpHelpers.InvokeHttpTrigger("RewindInstance", $"?instanceId={instanceId}");
        Assert.Equal(HttpStatusCode.PreconditionFailed, rewindResponse.StatusCode);
        response.Dispose();
        rewindResponse.Dispose();

        response = await HttpHelpers.InvokeHttpTrigger(
            "HttpStart_RewindOrchestration",
            $"?orchestrationName=RewindParentOrchestration&input=run&numFailures=0");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Running", 30);

        // Rewind a running orchestration
        rewindResponse = await HttpHelpers.InvokeHttpTrigger("RewindInstance", $"?instanceId={instanceId}");
        Assert.Equal(HttpStatusCode.PreconditionFailed, rewindResponse.StatusCode);
        response.Dispose();
        rewindResponse.Dispose();

        // Rewind a terminated orchestration
        response = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={instanceId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Terminated", 30);
        rewindResponse = await HttpHelpers.InvokeHttpTrigger("RewindInstance", $"?instanceId={instanceId}");
        Assert.Equal(HttpStatusCode.PreconditionFailed, rewindResponse.StatusCode);
        response.Dispose();
        rewindResponse.Dispose();

        // Rewind a pending orchestration
        response = await HttpHelpers.InvokeHttpTrigger(
           "HttpStart_RewindOrchestration",
           $"?orchestrationName=RewindParentOrchestration&input=complete&numFailures=0&delay=true");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Pending", 10);
        rewindResponse = await HttpHelpers.InvokeHttpTrigger("RewindInstance", $"?instanceId={instanceId}");
        Assert.Equal(HttpStatusCode.PreconditionFailed, rewindResponse.StatusCode);
        response.Dispose();
        rewindResponse.Dispose();

        // Now try to rewind a non-existent instance
        rewindResponse = await HttpHelpers.InvokeHttpTrigger("RewindInstance", $"?instanceId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, rewindResponse.StatusCode);
        response.Dispose();
        rewindResponse.Dispose();
    }
}
