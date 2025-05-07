// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class LargeOutputOrchestratorTests
{
    private readonly FunctionAppFixture fixture;
    private readonly ITestOutputHelper output;

    public LargeOutputOrchestratorTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(testOutputHelper);
        this.output = testOutputHelper;
    }

    [Theory]
    [InlineData(65)] // Provide a value slightly exceeding the 64 KB Azure Queue Storage limit to trigger use of blob storage instead at Azure Storage backend.
    public async Task LargeOutputStatusQueryTests(int sizeInKB)
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTriggerWithBody("LargeOutputOrchestrator_HttpStart", sizeInKB.ToString(), "application/json");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        string largeOutput = GenerateLargeString(sizeInKB);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        
        // Verify that large orchestrator outputs stored in blob storage are correctly returned via statusQueryGetUri
        Assert.Contains(largeOutput, orchestrationDetails.Output);
    }

    [Theory]
    [InlineData(4608)]// This value exceeds the default 4 MB, as the test sets the threshold to 6 MB.
    [Trait("DTS", "Skip")] 
    public async Task DurableTaskClientWriteOutputTests(int sizeInKB)
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTriggerWithBody("LargeOutputOrchestrator_HttpStart", sizeInKB.ToString(), "application/json");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        HttpResponseMessage result = await HttpHelpers.InvokeHttpTrigger("LargeOutputOrchestrator_Query_Output", $"?id={instanceId}");
        var expectedOutput = GenerateLargeString(sizeInKB);

        // Verify that large orchestrator outputs stored in blob storage are correctly returned when using OrchestrationMetada.ReadOutputAs()
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var content = await result.Content.ReadAsStringAsync();
        Assert.Contains(expectedOutput, content);
    }

    static string GenerateLargeString(int sizeInKB)
    {
        return new string('A', sizeInKB * 1024);
    }
}