// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class SuspendResumeTests
{
    private readonly FunctionAppFixture fixture;
    private readonly ITestOutputHelper output;

    public SuspendResumeTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(testOutputHelper);
        this.output = testOutputHelper;
    }


    [Fact]
    public async Task SuspendAndResumeRunningOrchestration_ShouldSucceed()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=LongRunningOrchestrator");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Running", 5);
        try
        {
            using HttpResponseMessage suspendResponse = await HttpHelpers.InvokeHttpTrigger("SuspendInstance", $"?instanceId={instanceId}");
            await AssertRequestSucceedsAsync(suspendResponse);

            await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Suspended", 5);

            using HttpResponseMessage resumeResponse = await HttpHelpers.InvokeHttpTrigger("ResumeInstance", $"?instanceId={instanceId}");
            await AssertRequestSucceedsAsync(resumeResponse);

            await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Running", 5);
        }
        finally
        {
            await TryTerminateInstanceAsync(instanceId);
        }
    }

    [Fact]
    public async Task SuspendSuspendedOrchestration_ShouldFail()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=LongRunningOrchestrator");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Running", 5);
        try
        {
            using HttpResponseMessage suspendResponse = await HttpHelpers.InvokeHttpTrigger("SuspendInstance", $"?instanceId={instanceId}");
            await AssertRequestSucceedsAsync(suspendResponse);

            await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Suspended", 5);

            using HttpResponseMessage resumeResponse = await HttpHelpers.InvokeHttpTrigger("SuspendInstance", $"?instanceId={instanceId}");
            await AssertRequestFailsAsync(resumeResponse, fixture.functionLanguageLocalizer.GetLocalizedStringValue("SuspendSuspendedInstance.FailureMessage"));

            // Give some time for Core Tools to write logs out
            Thread.Sleep(500);

            Assert.Contains(this.fixture.TestLogs.CoreToolsLogs, x => x.Contains("Cannot suspend orchestration instance in the Suspended state.") &&
                                                                x.Contains(instanceId));
        }
        finally
        {
            await TryTerminateInstanceAsync(instanceId);
        }
    }


    [Fact]
    public async Task ResumeRunningOrchestration_ShouldFail()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=LongRunningOrchestrator");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Running", 5);
        try
        {
            using HttpResponseMessage resumeResponse = await HttpHelpers.InvokeHttpTrigger("ResumeInstance", $"?instanceId={instanceId}");
            await this.AssertRequestFailsAsync(resumeResponse, fixture.functionLanguageLocalizer.GetLocalizedStringValue("ResumeRunningInstance.FailureMessage"));

            // Give some time for Core Tools to write logs out
            Thread.Sleep(500);

            Assert.Contains(this.fixture.TestLogs.CoreToolsLogs, x => x.Contains("Cannot resume orchestration instance in the Running state.") &&
                                                                x.Contains(instanceId));
        }
        finally
        {
            await TryTerminateInstanceAsync(instanceId);
        }
    }


    [Fact]
    public async Task SuspendResumeCompletedOrchestration_ShouldFail()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=HelloCities");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 5);
        try
        {
            using HttpResponseMessage suspendResponse = await HttpHelpers.InvokeHttpTrigger("SuspendInstance", $"?instanceId={instanceId}");
            await this.AssertRequestFailsAsync(suspendResponse, fixture.functionLanguageLocalizer.GetLocalizedStringValue("SuspendCompletedInstance.FailureMessage"));

            using HttpResponseMessage resumeResponse = await HttpHelpers.InvokeHttpTrigger("ResumeInstance", $"?instanceId={instanceId}");
            await this.AssertRequestFailsAsync(resumeResponse, fixture.functionLanguageLocalizer.GetLocalizedStringValue("ResumeCompletedInstance.FailureMessage"));

            // Give some time for Core Tools to write logs out
            Thread.Sleep(500);

            // For some reason, PowerShell does not log these warnings - instead the status code is 410 (Gone) with no log
            // when the instance is completed
            if (this.fixture.functionLanguageLocalizer.GetLanguageType() != LanguageType.PowerShell)
            {
                Assert.Contains(this.fixture.TestLogs.CoreToolsLogs, x => x.Contains("Cannot suspend orchestration instance in the Completed state.") &&
                                                                        x.Contains(instanceId));
                Assert.Contains(this.fixture.TestLogs.CoreToolsLogs, x => x.Contains("Cannot resume orchestration instance in the Completed state.") &&
                                                                        x.Contains(instanceId));
            }
        }
        finally
        {
            await TryTerminateInstanceAsync(instanceId);
        }
    }

    private async Task AssertRequestFailsAsync(HttpResponseMessage resumeResponse, string expectedErrorMessage)
    {
        Assert.Equal(HttpStatusCode.BadRequest, resumeResponse.StatusCode);

        string? responseMessage = await resumeResponse.Content.ReadAsStringAsync();
        Assert.NotNull(responseMessage);
        Assert.Equal(expectedErrorMessage, responseMessage);
    }

    private static async Task AssertRequestSucceedsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string? responseMessage = await response.Content.ReadAsStringAsync();
        Assert.NotNull(responseMessage);
        Assert.Empty(responseMessage);
    }

    private static async Task<bool> TryTerminateInstanceAsync(string instanceId)
    {
        try
        {
            // Clean up the instance by terminating it - no-op if this fails
            using HttpResponseMessage terminateResponse = await HttpHelpers.InvokeHttpTrigger("TerminateInstance", $"?instanceId={instanceId}");
            return true;
        }
        catch (Exception) { }
        return false;
    }
}
