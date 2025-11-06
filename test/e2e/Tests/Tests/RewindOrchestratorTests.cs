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
    [Trait("Java", "Skip")] // Rewind is not implemented in Java
    [Trait("Python", "Skip")] // Rewind is not implemented in Python
    [Trait("PowerShell", "Skip")] // Rewind is not implemented in PowerShell
    [InlineData(1)]
    [InlineData(2)]
    public async Task RewindFailedOrchestration_ShouldSucceed(int numFailures)
    {
        bool callEntities = this.fixture.GetDurabilityProvider() != FunctionAppFixture.ConfiguredDurabilityProviderType.MSSQL;
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(
            "HttpStart_RewindOrchestration",
            $"?input=fail&numFailures={numFailures}&callEntities={callEntities.ToString().ToLower()}");
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
    [Trait("Java", "Skip")] // Rewind is not implemented in Java
    [Trait("Python", "Skip")] // Rewind is not implemented in Python
    [Trait("PowerShell", "Skip")] // Rewind is not implemented in PowerShell
    public async Task RewindOnlyRewindsFailedOrchestrations()
    {
        // Try to rewind a completed, running, terminated, and pending orchestration - all should fail
        HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(
            "HttpStart_RewindOrchestration",
            $"?input=complete&numFailures=0&callEntities=false");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        // Rewind a completed orchestration
        HttpResponseMessage rewindResponse = await HttpHelpers.InvokeHttpTrigger("RewindInstance", $"?instanceId={instanceId}");
        // For all of the following tests, since Node throws a generic error in the case of a failure to rewind there is no great way 
        // to return specific status codes, whereas .NET isolated returns specific error types which can be used to return specific status codes.
        // So, in the Node case, we simply check for the BadRequest status code.
        if (this.fixture.functionLanguageLocalizer.GetLanguageType() == LanguageType.DotnetIsolated)
        {
            Assert.Equal(HttpStatusCode.PreconditionFailed, rewindResponse.StatusCode);
        }
        else
        {
            Assert.Equal(HttpStatusCode.BadRequest, rewindResponse.StatusCode);
        }
        response.Dispose();
        rewindResponse.Dispose();

        // Rewind a running orchestration
        response = await HttpHelpers.InvokeHttpTrigger(
            "HttpStart_RewindOrchestration",
            $"?input=run&numFailures=0&callEntities=false");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Running", 30);
        rewindResponse = await HttpHelpers.InvokeHttpTrigger("RewindInstance", $"?instanceId={instanceId}");
        if (this.fixture.functionLanguageLocalizer.GetLanguageType() == LanguageType.DotnetIsolated)
        {
            Assert.Equal(HttpStatusCode.PreconditionFailed, rewindResponse.StatusCode);
        }
        else
        {
            Assert.Equal(HttpStatusCode.BadRequest, rewindResponse.StatusCode);
        }
        response.Dispose();
        rewindResponse.Dispose();

        // Rewind a terminated orchestration
        response = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={instanceId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Terminated", 30);
        rewindResponse = await HttpHelpers.InvokeHttpTrigger("RewindInstance", $"?instanceId={instanceId}");
        if (this.fixture.functionLanguageLocalizer.GetLanguageType() == LanguageType.DotnetIsolated)
        {
            Assert.Equal(HttpStatusCode.PreconditionFailed, rewindResponse.StatusCode);
        }
        else
        {
            Assert.Equal(HttpStatusCode.BadRequest, rewindResponse.StatusCode);
        }
        response.Dispose();
        rewindResponse.Dispose();

        // Rewind a pending orchestration
        // Scheduled orchestrations are not implemented properly in Node, which is the only other language that has
        // rewind for now, so we just check for if the language is .NET isolated
        if (this.fixture.functionLanguageLocalizer.GetLanguageType() == LanguageType.DotnetIsolated)
        {
            response = await HttpHelpers.InvokeHttpTrigger(
               "HttpStart_RewindOrchestration",
               $"?input=complete&numFailures=0&callEntities=false&delay=true");
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
            statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
            await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Pending", 10);
            rewindResponse = await HttpHelpers.InvokeHttpTrigger("RewindInstance", $"?instanceId={instanceId}");
            Assert.Equal(HttpStatusCode.PreconditionFailed, rewindResponse.StatusCode);
            response.Dispose();
            rewindResponse.Dispose();
        }

        // Now try to rewind a non-existent instance
        rewindResponse = await HttpHelpers.InvokeHttpTrigger("RewindInstance", $"?instanceId={Guid.NewGuid()}");
        if (this.fixture.functionLanguageLocalizer.GetLanguageType() == LanguageType.DotnetIsolated)
        {
            Assert.Equal(HttpStatusCode.NotFound, rewindResponse.StatusCode);
        }
        else
        {
            Assert.Equal(HttpStatusCode.BadRequest, rewindResponse.StatusCode);
        }
        response.Dispose();
        rewindResponse.Dispose();
    }
}
