// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class HTTPFeatureTests
{
    private readonly FunctionAppFixture fixture;
    private readonly ITestOutputHelper output;

    public HTTPFeatureTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(testOutputHelper);
        this.output = testOutputHelper;
    }

    // This test schedules an orchestration that calls a URL using CallHttpAsync, which starts a long-running orchestration.
    // The URL initially returns a 202 Accepted response.
    // The test verifies that the orchestrator automatically polls the URL until it receives a non-202 response.
    [Fact]
    public async Task HTTPAutomaticPollingTests()
    {
        
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("HttpStart_HTTPPollingOrchestrator");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        // Wait a few minutes as the long-running orchestrator requires 2 minutes to finish.
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 150);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);

        // Ensure the final output includes the expected result, confirming that the orchestration
        // waited for the long-running HTTP call to complete rather than returning immediately.
        Assert.Contains("Long-running orchestration completed.", orchestrationDetails.Output);

        // Check that logs include evidence of HTTP polling behavior.
        Assert.Contains(this.fixture.TestLogs.CoreToolsLogs, x => x.Contains("Polling HTTP status at location"));
    }
}
