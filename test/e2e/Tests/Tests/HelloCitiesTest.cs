// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class HttpEndToEndTests
{
    private readonly FunctionAppFixture fixture;
    private readonly ITestOutputHelper output;

    public HttpEndToEndTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(testOutputHelper);
        this.output = testOutputHelper;
    }

    [Theory]
    [InlineData("HelloCities", HttpStatusCode.Accepted, "Hello Tokyo!")]
    public async Task HttpTriggerTests(string orchestrationName, HttpStatusCode expectedStatusCode, string partialExpectedOutput)
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", $"?orchestrationName={orchestrationName}");

        Assert.Equal(expectedStatusCode, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        Assert.Contains(partialExpectedOutput, orchestrationDetails.Output);
    }
}
