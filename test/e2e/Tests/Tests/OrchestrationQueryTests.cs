// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class OrchestrationQueryTests
{
    private readonly FunctionAppFixture _fixture;
    private readonly ITestOutputHelper _output;

    public OrchestrationQueryTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _fixture.TestLogs.UseTestLogger(testOutputHelper);
        _output = testOutputHelper;
    }


    [Fact]
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
    public async Task ListRunningOrchestrations_ShouldContainRunningOrchestration()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("LongOrchestrator_HttpStart", "");

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

            Assert.Contains(statusResponseJsonNode.AsArray(), x => x?["InstanceId"]?.ToString() == instanceId);
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
