// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class TimeoutTests
{
    private readonly FunctionAppFixture fixture;
    private readonly ITestOutputHelper output;

    public TimeoutTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(testOutputHelper);
        this.output = testOutputHelper;
    }

    [Theory]
    [InlineData(2, "The activity function timed out")]
    [InlineData(10, "The activity function completed successfully")]
    public async Task TimeoutFunction_ShouldTimeoutWhenAppropriate(int timeoutSeconds, string expectedOutput)
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("TimeoutOrchestrator_HttpStart", $"?timeoutSeconds={timeoutSeconds}");
        string actualMessage = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        Assert.Equal(expectedOutput, orchestrationDetails.Output);
    }
}
