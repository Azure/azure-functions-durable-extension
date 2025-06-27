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

    // Due to some kind of asynchronous race condition in XUnit, when running these tests in pipelines,
    // the output may be disposed before the message is written. Just ignore these types of errors for now. 
    private void WriteOutput(string message)
    {
        try
        {
            this.output.WriteLine(message);
        }
        catch
        {
            // Ignore
        }
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

    [Theory]
    [InlineData("HelloCities_HttpStart_Scheduled", 5, HttpStatusCode.Accepted)]
    [InlineData("HelloCities_HttpStart_Scheduled", -5, HttpStatusCode.Accepted)]
    [Trait("PowerShell", "Skip")] // Test not yet implemented in PowerShell
    public async Task ScheduledStartTests(string functionName, int startDelaySeconds, HttpStatusCode expectedStatusCode)
    {
        var testStartTime = DateTime.UtcNow;
        var scheduledStartTime = testStartTime + TimeSpan.FromSeconds(startDelaySeconds);
        string urlQueryString = $"?ScheduledStartTime={scheduledStartTime.ToString("o")}";

        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(functionName, urlQueryString);

        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        Assert.Equal(expectedStatusCode, response.StatusCode);

        if (scheduledStartTime > DateTime.UtcNow + TimeSpan.FromSeconds(1))
        {
            await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Pending", 30);
        }

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", Math.Max(startDelaySeconds, 0) + 30);

        // This +2s should not be necessary - however, experimentally the orchestration may run up to ~1 second before the scheduled time.
        // It is unclear currently whether this is a bug where orchestrations run early, or a clock difference/error,
        // but leaving this logic in for now until further investigation.
        Assert.True(DateTime.UtcNow + TimeSpan.FromSeconds(2) >= scheduledStartTime);

        var finalOrchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        WriteOutput($"Last updated at {finalOrchestrationDetails.LastUpdatedTime}, scheduled to complete at {scheduledStartTime}");
        Assert.True(finalOrchestrationDetails.LastUpdatedTime + TimeSpan.FromSeconds(2) >= scheduledStartTime);
    }
}
