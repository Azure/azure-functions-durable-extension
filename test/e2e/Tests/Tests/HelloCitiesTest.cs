// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class HttpEndToEndTests
{
    private readonly FunctionAppFixture _fixture;

    public HttpEndToEndTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _fixture.TestLogs.UseTestLogger(testOutputHelper);
    }

    [Theory]
    [InlineData("HelloCities_HttpStart", HttpStatusCode.Accepted)]
    public async Task HttpTriggerTests(string functionName, HttpStatusCode expectedStatusCode)
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(functionName, "");
        string actualMessage = await response.Content.ReadAsStringAsync();

        Assert.Equal(expectedStatusCode, response.StatusCode);
        Assert.False(string.IsNullOrEmpty(actualMessage));
    }

    [Theory]
    [InlineData("HelloCities_HttpStart_Scheduled", HttpStatusCode.Accepted)]
    public async Task ScheduledStartTests(string functionName, HttpStatusCode expectedStatusCode)
    {
        var scheduledStartDate = DateTime.Now + TimeSpan.FromSeconds(10);

        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(functionName, $"?ScheduledStartTime={scheduledStartDate.ToString("o")}");
        string actualMessage = await response.Content.ReadAsStringAsync();

        string statusQueryGetUri = DurableHelpers.ParseStatusQueryGetUri(response);

        Assert.Equal(expectedStatusCode, response.StatusCode);

        string startRuntimeStatus = DurableHelpers.GetRuntimeStatus(statusQueryGetUri);
        Assert.Equal("Pending", startRuntimeStatus);
        Thread.Sleep(11000);

        string endRuntimeStatus = DurableHelpers.GetRuntimeStatus(statusQueryGetUri);
        Assert.Equal("Completed", endRuntimeStatus);
    }
}
