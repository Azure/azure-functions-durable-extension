// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionSequentialName)]
public class OrchestrationQueryTests
{
    private readonly FunctionAppFixture fixture;
    private readonly ITestOutputHelper output;

    public OrchestrationQueryTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(testOutputHelper);
        this.output = testOutputHelper;
    }


    [Fact]
    [Trait("PowerShell", "Skip")] // PowerShell does not have a GetAllInstancesAsync equivalent today
    public async Task ListAllOrchestrations_ShouldSucceed()
    {
        using HttpResponseMessage statusResponse = await HttpHelpers.InvokeHttpTrigger("GetAllInstances", "");

        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        string? statusResponseMessage = await statusResponse.Content.ReadAsStringAsync();
        Assert.NotNull(statusResponseMessage);

        JsonNode? statusResponseJsonNode = JsonNode.Parse(statusResponseMessage);
        Assert.NotNull(statusResponseJsonNode);
    }


    [Fact]
    [Trait("PowerShell", "Skip")] // PowerShell does not have a GetRunningInstances equivalent today
    public async Task ListRunningOrchestrations_ShouldContainRunningOrchestration()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=LongRunningOrchestrator");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Running", 30);
        try
        {
            using HttpResponseMessage statusResponse = await HttpHelpers.InvokeHttpTrigger("GetRunningInstances", "");

            Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
            string? statusResponseMessage = await statusResponse.Content.ReadAsStringAsync();
            Assert.NotNull(statusResponseMessage);

            JsonNode? statusResponseJsonNode = JsonNode.Parse(statusResponseMessage);
            Assert.NotNull(statusResponseJsonNode);

            Assert.Contains(statusResponseJsonNode.AsArray(), x => x?["InstanceId"]?.ToString() == instanceId ||
                                                                   x?["instanceId"]?.ToString() == instanceId);
        }
        finally
        {
            await TryTerminateInstanceAsync(instanceId);
        }
    }
    private static async Task<bool> TryTerminateInstanceAsync(string instanceId)
    {
        try
        {
            // Clean up the instance by terminating it - no-op if this fails
            using HttpResponseMessage terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={instanceId}");
            return true;
        }
        catch (Exception) { }
        return false;
    }
}
