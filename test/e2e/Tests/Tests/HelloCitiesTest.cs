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
    private readonly ITestOutputHelper _output;

    public HttpEndToEndTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _fixture.TestLogs.UseTestLogger(testOutputHelper);
        _output = testOutputHelper;
    }

    // Due to some kind of asynchronous race condition in XUnit, when running these tests in pipelines,
    // the output may be disposed before the message is written. Just ignore these types of errors for now. 
    private void WriteOutput(string message)
    {
        try
        {
            _output.WriteLine(message);
        }
        catch
        {
            // Ignore
        }
    }

    [Theory]
    [InlineData("HelloCities_HttpStart", HttpStatusCode.Accepted, "Hello Tokyo!")]
    public async Task HttpTriggerTests(string functionName, HttpStatusCode expectedStatusCode, string partialExpectedOutput)
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(functionName, "");
        string actualMessage = await response.Content.ReadAsStringAsync();

        Assert.Equal(expectedStatusCode, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        Thread.Sleep(1000);
        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        Assert.Equal("Completed", orchestrationDetails.RuntimeStatus);
        Assert.Contains(partialExpectedOutput, orchestrationDetails.Output);
    }

    [Theory]
    [InlineData("HelloCities_HttpStart_Scheduled", 10, HttpStatusCode.Accepted)]
    [InlineData("HelloCities_HttpStart_Scheduled", -5, HttpStatusCode.Accepted)]
    public async Task ScheduledStartTests(string functionName, int startDelaySeconds, HttpStatusCode expectedStatusCode)
    {
        var testStartTime = DateTime.UtcNow;
        var scheduledStartTime = testStartTime + TimeSpan.FromSeconds(startDelaySeconds);
        string urlQueryString = $"?ScheduledStartTime={scheduledStartTime.ToString("o")}";

        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(functionName, urlQueryString);
        string actualMessage = await response.Content.ReadAsStringAsync();

        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        Assert.Equal(expectedStatusCode, response.StatusCode);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        while (DateTime.UtcNow < scheduledStartTime + TimeSpan.FromSeconds(-1))
        {
            WriteOutput($"Test scheduled for {scheduledStartTime}, current time {DateTime.Now}");
            orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
            Assert.Equal("Pending", orchestrationDetails.RuntimeStatus);
            Thread.Sleep(1000);
        }

        // Give a small amount of time for the orchestration to complete, even if scheduled to run immediately
        Thread.Sleep(3000);
        WriteOutput($"Test scheduled for {scheduledStartTime}, current time {DateTime.Now}, looking for completed");
        var finalOrchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        int retryAttempts = 0;
        while (finalOrchestrationDetails.RuntimeStatus != "Completed" && retryAttempts < 10)
        {
            Thread.Sleep(1000);
            finalOrchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
            retryAttempts++;
        }
        Assert.Equal("Completed", finalOrchestrationDetails.RuntimeStatus);

        Assert.True(finalOrchestrationDetails.LastUpdatedTime > scheduledStartTime);
    }
}
