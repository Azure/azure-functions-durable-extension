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

    public HttpEndToEndTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(testOutputHelper);
        this.output = testOutputHelper;
    }

    [Fact]
    public async Task HTTPAutomaticPollingTests()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("HttpStart_HTTPPollingOrchestrator");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 150);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);

        // Verify that the output contains the LongRunningOrchestrator's result,
        // ensuring the orchestrator completed and did not just return a 202 Accepted.
        Assert.Contains("Hello Tokyo", orchestrationDetails.Output);
    }
}
