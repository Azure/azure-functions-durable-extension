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
    [Trait("MSSQL", "Skip")] // This test fails for MSSQL unless this bug is fixed: https://github.com/microsoft/durabletask-mssql/issues/287
    [Trait("DTS", "Skip")] // DTS will fail this test unless this bug is fixed: https://msazure.visualstudio.com/Antares/_workitems/edit/31779638
    public async Task OrchestratorWithUncaughtActivityException_ShouldFail()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("RethrowActivityException_HttpStart", "");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Failed", 30);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        
        Assert.StartsWith("Microsoft.DurableTask.TaskFailedException", orchestrationDetails.Output);
        Assert.Contains("This activity failed", orchestrationDetails.Output);
    }

    [Fact]
    [Trait("MSSQL", "Skip")] // Durable Entities are not supported in MSSQL/Dotnet Isolated, see https://github.com/microsoft/durabletask-mssql/issues/205
    [Trait("DTS", "Skip")] // DTS will fail this test unless this bug is fixed: https://msazure.visualstudio.com/Antares/_workitems/edit/31779638
    public async Task OrchestratorWithUncaughtEntityException_ShouldFail()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("RethrowEntityException_HttpStart", "");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Failed", 30);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        
        Assert.StartsWith("Microsoft.DurableTask.Entities.EntityOperationFailedException", orchestrationDetails.Output);
        Assert.Contains("This entity failed", orchestrationDetails.Output);
    }

    [Fact]
    public async Task OrchestratorWithCaughtActivityException_ShouldSucceed()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("CatchActivityException_HttpStart", "");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        Assert.StartsWith("Task 'RaiseException' (#0) failed with an unhandled exception:", orchestrationDetails.Output);
        Assert.Contains("This activity failed", orchestrationDetails.Output);
    }

    [Fact]
    [Trait("MSSQL", "Skip")] // Durable Entities are not supported in MSSQL/Dotnet Isolated, see https://github.com/microsoft/durabletask-mssql/issues/205
    public async Task OrchestratorWithCaughtEntityException_ShouldSucceed()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("CatchEntityException_HttpStart", "");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        Assert.StartsWith("Operation 'ThrowFirstTimeOnly' of entity '@counter@MyExceptionEntity' failed:", orchestrationDetails.Output);
        Assert.Contains("This entity failed", orchestrationDetails.Output);
        Assert.Contains("More information about the failure", orchestrationDetails.Output);

        // For now, we deliberately do not return inner exception details on entity failure. 
        // If this changes in the future, update this test. 
        Assert.DoesNotContain("Inner exception message", orchestrationDetails.Output);
    }

    [Fact]
    public async Task OrchestratorWithRetriedActivityException_ShouldSucceed()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("RetryActivityException_HttpStart", "");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        Assert.Equal("Success", orchestrationDetails.Output);

        // Give some time for Core Tools to write logs out
        Thread.Sleep(500);

        Assert.Contains(_fixture.TestLogs.CoreToolsLogs, x => x.Contains(nameof(InvalidOperationException)) &&
                                                              x.Contains("This activity failed"));
    }

    [Fact]
    [Trait("MSSQL", "Skip")] // Durable Entities are not supported in MSSQL/Dotnet Isolated, see https://github.com/microsoft/durabletask-mssql/issues/205
    [Trait("DTS", "Skip")] // DTS will fail this test unless this issue is fixed, see https://msazure.visualstudio.com/Antares/_workitems/edit/31778744
    public async Task OrchestratorWithRetriedEntityException_ShouldSucceed()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("RetryEntityException_HttpStart", "");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        Assert.Equal("Success", orchestrationDetails.Output);

        // Give some time for Core Tools to write logs out
        Thread.Sleep(500);

        // For entities, these logs are not emitted as one continuous log, but each line of the exception .ToString() is
        // logged individually.
        Assert.Contains(_fixture.TestLogs.CoreToolsLogs, x => x.Contains(nameof(InvalidOperationException)) &&
                                                              x.Contains("This entity failed"));
        Assert.Contains(_fixture.TestLogs.CoreToolsLogs, x => x.Contains("More information about the failure"));
        Assert.Contains(_fixture.TestLogs.CoreToolsLogs, x => x.Contains(nameof(OverflowException)) &&
                                                              x.Contains("Inner exception message"));
    }

    [Fact]
    public async Task OrchestratorWithCustomRetriedActivityException_ShouldSucceed()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("CustomRetryActivityException_HttpStart", "");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        Assert.Equal("Success", orchestrationDetails.Output);

        // Give some time for Core Tools to write logs out
        Thread.Sleep(500);

        // We want to ensure that multiline exception messages and inner exceptions are preserved
        Assert.Contains(_fixture.TestLogs.CoreToolsLogs, x => x.Contains(nameof(InvalidOperationException)) &&
                                                              x.Contains(nameof(OverflowException)) &&
                                                              x.Contains("This activity failed") &&
                                                              x.Contains("More information about the failure"));
    }
}
