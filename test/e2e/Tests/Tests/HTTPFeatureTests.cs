// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class HttpFeatureTests
{
    private readonly FunctionAppFixture fixture;
    private readonly ITestOutputHelper output;

    public HttpFeatureTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(testOutputHelper);
        this.output = testOutputHelper;
    }

    // This test schedules an orchestration that calls a URL using CallHttpAsync, which starts a long-running orchestration.
    // The URL initially returns a 202 Accepted response.
    // The test verifies that the orchestrator automatically polls the URL until it receives a non-202 response.
    [Fact]
    [Trait("DTS", "Skip")] // DTS will timeout this test. Need to fix it later. 
    [Trait("PowerShell", "Skip")] // HTTP automatic polling is not yet implemented in PowerShell
    public async Task HttpAutomaticPollingTests()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("HttpStart_HttpPollingOrchestrator");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        // Wait here as the long-running orchestration requires about 1 minutes to finish.
        // Set wait time to be 150 seconds becasue the DTS CI takes more time to finish. 
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 150);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);

        // Ensure the final output includes the expected result, confirming that the orchestration
        // waited for the long-running HTTP call to complete rather than returning immediately.
        Assert.Contains("Long-running orchestration completed.", orchestrationDetails.Output);

        // Check that logs include evidence of HTTP polling behavior.
        Assert.Contains(this.fixture.TestLogs.CoreToolsLogs, x => x.Contains("Polling HTTP status at location"));
    }

    [Fact]
    // Tests HTTP call using managed identity credentials.
    // Note: Currently uses DefaultAzureCredential based on available information.
    // Since GitHub CI doesn't support this, the orchestrator will fail in CI but succeed locally.
    // Therefore, the test verifies results conditionally based on the execution environment.
    [Trait("DTS", "Skip")] // DTS will timeout this test, probably an undiscovered issue. Skip for now.
    [Trait("PowerShell", "Skip")] // Managed identity HTTP calls not supported in PowerShell
    public async Task HttpCallWithTokenSourceTest()
    {   
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=HttpWithTokenSourceOrchestrator");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 60);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);

        // Check if we're running in GitHub CI
        bool isGitHubCI = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));
        
        if (isGitHubCI)
        {
            // In GitHub CI, verify that the error message indicates failure due to absence of valid token credentials.
            // Check output to verify CallHttpAsync fails.
            Assert.Contains("Token source HTTP call failed", orchestrationDetails.Output);

            // Check that logs to verify orchestrator fails becasue of credential failure. 
            Assert.Contains(this.fixture.TestLogs.CoreToolsLogs, log =>
                log.Contains("Task 'BuiltIn::HttpActivity' (#0) failed with an unhandled exception: DefaultAzureCredential failed to retrieve a token from the included credentials."));

            Assert.Contains(this.fixture.TestLogs.CoreToolsLogs, log =>
                log.Contains("WorkloadIdentityCredential authentication unavailable"));

            Assert.Contains(this.fixture.TestLogs.CoreToolsLogs, log =>
                log.Contains("ManagedIdentityCredential authentication unavailable."));

            Assert.Contains(this.fixture.TestLogs.CoreToolsLogs, log =>
                log.Contains("EnvironmentCredential authentication unavailable."));
        }
        else
        {
            // If run locally, this test should compelete successfully. 
            Assert.Contains("Token source HTTP call completed successfully", orchestrationDetails.Output);
        }
    }
}
