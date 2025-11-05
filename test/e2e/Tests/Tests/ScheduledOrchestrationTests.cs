// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class ScheduledOrchestrationTests
{
    private readonly FunctionAppFixture fixture;
    private readonly ITestOutputHelper output;

    public ScheduledOrchestrationTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
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
    [InlineData("HelloCities_HttpStart_Scheduled", 10, HttpStatusCode.Accepted)]
    [InlineData("HelloCities_HttpStart_Scheduled", -5, HttpStatusCode.Accepted)]
    [Trait("PowerShell", "Skip")] // Scheduled orchestrations not implemented in PowerShell
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
            if (this.fixture.functionLanguageLocalizer.GetLanguageType() == LanguageType.DotnetIsolated ||
                this.fixture.functionLanguageLocalizer.GetLanguageType() == LanguageType.Java)
            {
                // This line will throw if the orchestration goes to a terminal state before reaching "Pending",
                // ensuring that any scheduled orchestrations that run immediately fail the test.
                await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Pending", 30);
            }
            else
            {
                // Scheduled orchestrations are not properly implemented in the other languages - however, 
                // this test has been implemented using timers in the orchestration instead.
                await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Running", 30);
            }
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

    [Theory]
    [InlineData("EntityCreatesScheduledOrchestrationOrchestrator_HttpStart", 10, HttpStatusCode.Accepted)]
    [InlineData("EntityCreatesScheduledOrchestrationOrchestrator_HttpStart", -5, HttpStatusCode.Accepted)]
    [Trait("PowerShell", "Skip")] // Durable Entities not yet implemented in PowerShell
    [Trait("Java", "Skip")] // Durable Entities not yet implemented in Java
    [Trait("Python", "Skip")] // Durable Entities do not support the "schedule new orchestration" action in Python
    [Trait("Node", "Skip")] // Durable Entities do not support the "schedule new orchestration" action in Node
    [Trait("MSSQL", "Skip")] // Durable Entities are not supported in MSSQL for out-of-proc (see https://github.com/microsoft/durabletask-mssql/issues/205)
    public async Task ScheduledStartFromEntitiesTests(string functionName, int startDelaySeconds, HttpStatusCode expectedStatusCode)
    {
        var testStartTime = DateTime.UtcNow;
        var scheduledStartTime = testStartTime + TimeSpan.FromSeconds(startDelaySeconds);
        string urlQueryString = $"?scheduledStartDelaySeconds={startDelaySeconds}";

        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(functionName, urlQueryString);

        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        Assert.Equal(expectedStatusCode, response.StatusCode);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", Math.Max(startDelaySeconds, 0) + 30);
        var schedulerOrchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        string subOrchestratorInstanceId = schedulerOrchestrationDetails.Output;

        string subOrchestratorStatusQueryGetUri = statusQueryGetUri.ToLower().Replace(schedulerOrchestrationDetails.InstanceId.ToLower(), subOrchestratorInstanceId);

        // Azure Storage backend has a quirk where creating an orchestration from an entity creates the OrchestrationStarted event in the History table
        // but doesn't initialize the orchestration state in the Instances table until the orchestration starts running. Since the implementation for
        // GetOrchestrationStateAsync in AzureStorage only queries the Instances table, this will 404 until the orchestration state becomes "running",
        // so we can't check for "Pending".
        // The checks below should still suffice to prove that the orchestration did not run until the scheduled time.
        if (scheduledStartTime > DateTime.UtcNow + TimeSpan.FromSeconds(1) && this.fixture.GetDurabilityProvider() != FunctionAppFixture.ConfiguredDurabilityProviderType.AzureStorage)
        {
            // This line will throw if the orchestration goes to a terminal state before reaching "Pending",
            // ensuring that any scheduled orchestrations that run immediately fail the test.
            await DurableHelpers.WaitForOrchestrationStateAsync(subOrchestratorStatusQueryGetUri, "Pending", 30);
        }

        await DurableHelpers.WaitForOrchestrationStateAsync(subOrchestratorStatusQueryGetUri, "Completed", Math.Max(startDelaySeconds, 0) + 30);

        // This +2s should not be necessary - however, experimentally the orchestration may run up to ~1 second before the scheduled time.
        // It is unclear currently whether this is a bug where orchestrations run early, or a clock difference/error,
        // but leaving this logic in for now until further investigation.
        Assert.True(DateTime.UtcNow + TimeSpan.FromSeconds(2) >= scheduledStartTime);

        var subOrchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(subOrchestratorStatusQueryGetUri);
        Assert.True(subOrchestrationDetails.LastUpdatedTime + TimeSpan.FromSeconds(2) >= scheduledStartTime);
        Assert.Equal("Success", subOrchestrationDetails.Output);
    }
}
