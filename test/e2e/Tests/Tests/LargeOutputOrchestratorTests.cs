// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class LargeOutputOrcehstratorTests
{
    private readonly FunctionAppFixture _fixture;
    private readonly ITestOutputHelper _output;

    public LargeOutputOrcehstratorTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _fixture.TestLogs.UseTestLogger(testOutputHelper);
        _output = testOutputHelper;
    }

    [Fact] 
    public async Task LargeOutputStatusQueryTests()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("LargeOutputOrchestrator_HttpStart", "");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        string largeOutput = new string('A', 65 * 1024);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        
        // Verify that large orchestrator outputs stored in blob storage are correctly returned via statusQueryGetUri
        Assert.Contains(largeOutput, orchestrationDetails.Output);
    }

    [Fact]
    public async Task DurableTaskClientWriteOutputTests()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("LargeOutputOrchestrator_HttpStart", "");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        HttpResponseMessage result = await HttpHelpers.InvokeHttpTrigger("LargeOutputOrchestrator_Query_Output", $"?id={instanceId}");
        var expectedOutput = new string('A', 65 * 1024);

        // Verify that large orchestrator outputs stored in blob storage are correctly returned when using OrchestrationMetada.ReadOutputAs()
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var content = await result.Content.ReadAsStringAsync();
        Assert.Contains(expectedOutput, content);
    }
}