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
        LanguageType languageType = this.fixture.functionLanguageLocalizer.GetLanguageType();

        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=HelloCities");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 5);
        try
        {
            using HttpResponseMessage suspendResponse = await HttpHelpers.InvokeHttpTrigger("SuspendInstance", $"?instanceId={instanceId}");
            using HttpResponseMessage resumeResponse = await HttpHelpers.InvokeHttpTrigger("ResumeInstance", $"?instanceId={instanceId}");

            if (languageType == LanguageType.Python || languageType == LanguageType.Node)
            {
                // In python and node, suspending or resuming a completed, failed, or terminated instance swallows the failure
                // and acts as if the instance was suspended/resumed successfully. This might be a consistency issue, but is it
                // a bug?
                // see https://github.com/Azure/azure-functions-durable-python/blob/97a0891f80ccb4cb357e9f39b79a4eb4326f6d98/azure/durable_functions/models/DurableOrchestrationClient.py#L747
                // see https://github.com/Azure/azure-functions-durable-python/blob/97a0891f80ccb4cb357e9f39b79a4eb4326f6d98/azure/durable_functions/models/DurableOrchestrationClient.py#L782
                await AssertRequestSucceedsAsync(suspendResponse);

                await AssertRequestSucceedsAsync(resumeResponse);
            }
            else
            {
                await this.AssertRequestFailsAsync(suspendResponse, fixture.functionLanguageLocalizer.GetLocalizedStringValue("SuspendCompletedInstance.FailureMessage"));

                await this.AssertRequestFailsAsync(resumeResponse, fixture.functionLanguageLocalizer.GetLocalizedStringValue("ResumeCompletedInstance.FailureMessage"));
            }

            // Give some time for Core Tools to write logs out
            Thread.Sleep(500);

            // PowerShell, Python, Node all use the HTTP suspend/resume APIs, which return 410 (Gone) and do not log
            // when the instance is completed
            if (languageType != LanguageType.PowerShell && languageType != LanguageType.Python && languageType != LanguageType.Node)
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
        Assert.StartsWith(expectedErrorMessage, responseMessage);
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
