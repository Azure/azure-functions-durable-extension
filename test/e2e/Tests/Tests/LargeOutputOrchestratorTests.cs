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

    [Theory]
    [InlineData(4608)] // This value exceeds the default 4 MB, as the test sets the threshold to 5 MB.
    public async Task LargeOutputStatusQueryTests(int size)
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTriggerWithBody("LargeOutputOrchestrator_HttpStart", size.ToString(), "application/json");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        string largeOutput = GenerateLargeString(size);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        
        // Verify that large orchestrator outputs stored in blob storage are correctly returned via statusQueryGetUri
        Assert.Contains(largeOutput, orchestrationDetails.Output);
    }

    [Theory]
    [InlineData(4608)]// This value exceeds the default 4 MB, as the test sets the threshold to 5 MB.
    public async Task DurableTaskClientWriteOutputTests(int size)
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTriggerWithBody("LargeOutputOrchestrator_HttpStart", size.ToString(), "application/json");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        HttpResponseMessage result = await HttpHelpers.InvokeHttpTrigger("LargeOutputOrchestrator_Query_Output", $"?id={instanceId}");
        var expectedOutput = GenerateLargeString(size);

        // Verify that large orchestrator outputs stored in blob storage are correctly returned when using OrchestrationMetada.ReadOutputAs()
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var content = await result.Content.ReadAsStringAsync();
        Assert.Contains(expectedOutput, content);
    }

    static string GenerateLargeString(int size)
    {
        return new string('A', size * 1024);
    }
}