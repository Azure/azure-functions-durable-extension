// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class ErrorHandlingTests
{
    private readonly FunctionAppFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ErrorHandlingTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _fixture.TestLogs.UseTestLogger(testOutputHelper);
        _output = testOutputHelper;
    }

    [Fact]
    public async Task OrchestratorWithUncaughtActivityException_ShouldFail()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("RethrowActivityException_HttpStart", "");
        string actualMessage = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Failed", 30);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        Assert.StartsWith("Microsoft.DurableTask.TaskFailedException", orchestrationDetails.Output);
        Assert.Contains("This activity failed", orchestrationDetails.Output);
    }

    [Fact]
    public async Task OrchestratorWithCaughtActivityException_ShouldSucced()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("CatchActivityException_HttpStart", "");
        string actualMessage = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        Assert.StartsWith("Task 'RaiseException' (#0) failed with an unhandled exception:", orchestrationDetails.Output);
        Assert.Contains("This activity failed", orchestrationDetails.Output);
    }

    [Fact]
    public async Task OrchestratorWithRetriedActivityException_ShouldSucced()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("RetryActivityException_HttpStart", "");
        string actualMessage = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 10);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        Assert.Equal("Success", orchestrationDetails.Output);

        // Give some time for Core Tools to write logs out
        Thread.Sleep(500);

        Assert.Contains(_fixture.TestLogs.CoreToolsLogs, x => x.Contains(nameof(InvalidOperationException)) &&
                                                              x.Contains("This activity failed"));
    }

    [Fact]
    public async Task OrchestratorWithCustomRetriedActivityException_ShouldSucced()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("CustomRetryActivityException_HttpStart", "");
        string actualMessage = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 10);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        Assert.Equal("Success", orchestrationDetails.Output);

        // Give some time for Core Tools to write logs out
        Thread.Sleep(500);

        Assert.Contains(_fixture.TestLogs.CoreToolsLogs, x => x.Contains(nameof(InvalidOperationException)) &&
                                                              x.Contains("This activity failed"));
    }
}
