// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class TerminateOrchestratorTests
{
    private readonly FunctionAppFixture fixture;
    private readonly ITestOutputHelper output;

    public TerminateOrchestratorTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(testOutputHelper);
        this.output = testOutputHelper;
    }


    [Fact]
    [Trait("Java-MSSQL", "Skip")] // Bug: https://github.com/microsoft/durabletask-java/issues/237
    public async Task TerminateRunningOrchestration_ShouldSucceed()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=LongRunningOrchestrator");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Running", 30);

        using HttpResponseMessage terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={instanceId}");
        await AssertTerminateRequestSucceedsAsync(terminateResponse);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Terminated", 30);
    }


    [Fact(Skip = "Will enable when https://github.com/Azure/azure-functions-durable-extension/issues/3025 is fixed")]
    [Trait("PowerShell", "Skip")] // Scheduled orchestrations not implemented in PowerShell
    public async Task TerminateScheduledOrchestration_ShouldSucceed()
    {
        DateTime scheduledStartTime = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("HelloCities_HttpStart_Scheduled", $"?scheduledStartTime={scheduledStartTime.ToString("o")}");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Pending", 30);

        using HttpResponseMessage terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={instanceId}");
        await AssertTerminateRequestSucceedsAsync(terminateResponse);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Terminated", 30);
    }


    [Fact]
    [Trait("Java-MSSQL", "Skip")] // Bug: https://github.com/microsoft/durabletask-java/issues/237
    public async Task TerminateTerminatedOrchestration_ShouldFail()
    {
        LanguageType languageType = this.fixture.functionLanguageLocalizer.GetLanguageType();

        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=LongRunningOrchestrator");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Running", 30);

        using HttpResponseMessage terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={instanceId}");
        await AssertTerminateRequestSucceedsAsync(terminateResponse);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Terminated", 30);

        using HttpResponseMessage terminateAgainResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={instanceId}");

        if (languageType == LanguageType.Python || languageType == LanguageType.Node)
        {
            // In python and Node, terminating a completed, failed, or terminated instance swallows the failure
            // and acts as if the instance was terminated successfully. This might be a consistency issue, but is it
            // a bug?
            // see https://github.com/Azure/azure-functions-durable-python/blob/97a0891f80ccb4cb357e9f39b79a4eb4326f6d98/azure/durable_functions/models/DurableOrchestrationClient.py#L444
            Assert.Equal(HttpStatusCode.OK, terminateAgainResponse.StatusCode);
        }
        else
        {
            Assert.Equal(HttpStatusCode.BadRequest, terminateAgainResponse.StatusCode);
        }

        // Check the exception returned contains the right statusCode and message. 
        string? terminateAgainResponseMessage = await terminateAgainResponse.Content.ReadAsStringAsync();
        Assert.NotNull(terminateAgainResponseMessage);

        Assert.Contains(fixture.functionLanguageLocalizer.GetLocalizedStringValue("TerminateTerminatedInstance.FailureMessage", instanceId), terminateAgainResponseMessage);

        // Give some time for Core Tools to write logs out
        Thread.Sleep(500);

        // PowerShell, Python, Node all use the HTTP terminate API, which returns 410 (Gone) and does not log
        // when the instance is completed
        if (languageType == LanguageType.DotnetIsolated)
        {
            Assert.Contains("StatusCode=\"FailedPrecondition\"", terminateAgainResponseMessage);
            Assert.Contains(this.fixture.TestLogs.CoreToolsLogs, x => x.Contains("Cannot terminate orchestration instance in the Terminated state.") &&
                                                              x.Contains(instanceId));
        }
        else if (languageType == LanguageType.Java)
        {
            Assert.Contains("FAILED_PRECONDITION: InvalidOperationException", terminateAgainResponseMessage);
            Assert.Contains(this.fixture.TestLogs.CoreToolsLogs, x => x.Contains("Cannot terminate orchestration instance in the Terminated state.") &&
                                                              x.Contains(instanceId));
        }
    }


    [Fact]
    public async Task TerminateCompletedOrchestration_ShouldFail()
    {
        LanguageType languageType = this.fixture.functionLanguageLocalizer.GetLanguageType();

        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=HelloCities");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        using HttpResponseMessage terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={instanceId}");

        if (languageType == LanguageType.Python || languageType == LanguageType.Node)
        {
            // In python and Node, terminating a completed, failed, or terminated instance swallows the failure
            // and acts as if the instance was terminated successfully. This might be a consistency issue, but is it
            // a bug?
            // see https://github.com/Azure/azure-functions-durable-python/blob/97a0891f80ccb4cb357e9f39b79a4eb4326f6d98/azure/durable_functions/models/DurableOrchestrationClient.py#L444
            Assert.Equal(HttpStatusCode.OK, terminateResponse.StatusCode);
        }
        else
        {
            Assert.Equal(HttpStatusCode.BadRequest, terminateResponse.StatusCode);
        }

        string? terminateResponseMessage = await terminateResponse.Content.ReadAsStringAsync();
        Assert.NotNull(terminateResponseMessage);

        Assert.Contains(fixture.functionLanguageLocalizer.GetLocalizedStringValue("TerminateCompletedInstance.FailureMessage", instanceId), terminateResponseMessage);

        // Give some time for Core Tools to write logs out
        Thread.Sleep(500);

        // PowerShell, Python, Node all use the HTTP terminate API, which returns 410 (Gone) and does not log
        // when the instance is completed
        if (languageType == LanguageType.DotnetIsolated)
        {
            Assert.Contains("StatusCode=\"FailedPrecondition\"", terminateResponseMessage);
            Assert.Contains(this.fixture.TestLogs.CoreToolsLogs, x => x.Contains("Cannot terminate orchestration instance in the Completed state.") &&
                                                                  x.Contains(instanceId));
        }
        else if (languageType == LanguageType.Java)
        {
            Assert.Contains("FAILED_PRECONDITION: InvalidOperationException", terminateResponseMessage);
            Assert.Contains(this.fixture.TestLogs.CoreToolsLogs, x => x.Contains("Cannot terminate orchestration instance in the Completed state.") &&
                                                                  x.Contains(instanceId));
        }
    }

    [Fact]
    public async Task TerminateNonExistantOrchestration_ShouldFail()
    {
        LanguageType languageType = this.fixture.functionLanguageLocalizer.GetLanguageType();
        string instanceId = Guid.NewGuid().ToString();
        using HttpResponseMessage terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={instanceId}");
        Assert.Equal(HttpStatusCode.BadRequest, terminateResponse.StatusCode);

        string? terminateResponseMessage = await terminateResponse.Content.ReadAsStringAsync();
        Assert.NotNull(terminateResponseMessage);

        // Check the exception returned contains the right statusCode and message. 
        // This particular part of the error is not emitted in Python, PowerShell, Node
        if (languageType == LanguageType.DotnetIsolated)
        {
            Assert.Contains("Status(StatusCode=\"NotFound\"", terminateResponseMessage);
        }
        else if (languageType == LanguageType.Java)
        {
            Assert.Contains("NOT_FOUND: ArgumentException: No instance", terminateResponseMessage);
        }
        Assert.Contains(fixture.functionLanguageLocalizer.GetLocalizedStringValue("TerminateInvalidInstance.FailureMessage", instanceId), terminateResponseMessage);
    }

    private static async Task AssertTerminateRequestSucceedsAsync(HttpResponseMessage terminateResponse)
    {
        Assert.Equal(HttpStatusCode.OK, terminateResponse.StatusCode);

        string? terminateResponseMessage = await terminateResponse.Content.ReadAsStringAsync();
        Assert.NotNull(terminateResponseMessage);
        Assert.Empty(terminateResponseMessage);
    }
}
