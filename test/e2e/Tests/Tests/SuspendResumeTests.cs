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

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Running", 15);
        try
        {
            using HttpResponseMessage suspendResponse = await HttpHelpers.InvokeHttpTrigger("SuspendInstance", $"?instanceId={instanceId}");
            await AssertRequestSucceedsAsync(suspendResponse);

            await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Suspended", 15);

            using HttpResponseMessage resumeResponse = await HttpHelpers.InvokeHttpTrigger("ResumeInstance", $"?instanceId={instanceId}");
            await AssertRequestSucceedsAsync(resumeResponse);

            await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Running", 30);

            // Verify that the ClientOperationReceived logs were emitted with a FunctionInvocationId
            ClientOperationLogHelpers.AssertClientOperationLogExists(
                () => this.fixture.TestLogs.CoreToolsLogs,
                "StartOrchestration",
                instanceId,
                this.fixture.functionLanguageLocalizer.GetLanguageType());
            ClientOperationLogHelpers.AssertClientOperationLogExists(
                () => this.fixture.TestLogs.CoreToolsLogs,
                "Suspend",
                instanceId,
                this.fixture.functionLanguageLocalizer.GetLanguageType());
            ClientOperationLogHelpers.AssertClientOperationLogExists(
                () => this.fixture.TestLogs.CoreToolsLogs,
                "Resume",
                instanceId,
                this.fixture.functionLanguageLocalizer.GetLanguageType());
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

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Running", 15);
        try
        {
            using HttpResponseMessage suspendResponse = await HttpHelpers.InvokeHttpTrigger("SuspendInstance", $"?instanceId={instanceId}");
            await AssertRequestSucceedsAsync(suspendResponse);

            await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Suspended", 15);

            using HttpResponseMessage resumeResponse = await HttpHelpers.InvokeHttpTrigger("SuspendInstance", $"?instanceId={instanceId}");
            await AssertRequestFailsAsync(resumeResponse, fixture.functionLanguageLocalizer.GetLocalizedStringValue("SuspendSuspendedInstance.FailureMessage", instanceId));

            await this.fixture.TestLogs.AssertLogExistsAsync(
                x => x.Contains("Cannot suspend orchestration instance in the Suspended state.") && x.Contains(instanceId),
                $"Expected 'Cannot suspend orchestration instance in the Suspended state.' log for instance '{instanceId}' was not found.");
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

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Running", 15);

        try
        {
            using HttpResponseMessage resumeResponse = await HttpHelpers.InvokeHttpTrigger("ResumeInstance", $"?instanceId={instanceId}");
            await this.AssertRequestFailsAsync(resumeResponse, fixture.functionLanguageLocalizer.GetLocalizedStringValue("ResumeRunningInstance.FailureMessage", instanceId));

            await this.fixture.TestLogs.AssertLogExistsAsync(
                x => x.Contains("Cannot resume orchestration instance in the Running state.") && x.Contains(instanceId),
                $"Expected 'Cannot resume orchestration instance in the Running state.' log for instance '{instanceId}' was not found.");
        }
        finally
        {
            await TryTerminateInstanceAsync(instanceId);
        }
    }


    [Fact]
    [Trait("Node", "Skip")] // Suspend of a non-existent instance uses the HTTP API for these workers, which is unaffected by this gRPC-server fix (see microsoft/durabletask-js#315 / this PR)
    [Trait("Python", "Skip")]
    [Trait("PowerShell", "Skip")]
    public async Task SuspendNonExistentOrchestration_ShouldFail()
    {
        LanguageType languageType = this.fixture.functionLanguageLocalizer.GetLanguageType();
        string instanceId = Guid.NewGuid().ToString();

        using HttpResponseMessage suspendResponse = await HttpHelpers.InvokeHttpTrigger("SuspendInstance", $"?instanceId={instanceId}");
        Assert.Equal(HttpStatusCode.BadRequest, suspendResponse.StatusCode);

        string? responseMessage = await suspendResponse.Content.ReadAsStringAsync();
        Assert.NotNull(responseMessage);

        if (languageType == LanguageType.DotnetIsolated)
        {
            Assert.Contains("Status(StatusCode=\"NotFound\"", responseMessage);
        }
        else if (languageType == LanguageType.Java)
        {
            Assert.Contains("NOT_FOUND: ArgumentException: No instance", responseMessage);
        }

        Assert.Contains(fixture.functionLanguageLocalizer.GetLocalizedStringValue("SuspendInvalidInstance.FailureMessage", instanceId), responseMessage);
    }

    [Fact]
    [Trait("Node", "Skip")] // Resume of a non-existent instance uses the HTTP API for these workers, which is unaffected by this gRPC-server fix (see microsoft/durabletask-js#315 / this PR)
    [Trait("Python", "Skip")]
    [Trait("PowerShell", "Skip")]
    public async Task ResumeNonExistentOrchestration_ShouldFail()
    {
        LanguageType languageType = this.fixture.functionLanguageLocalizer.GetLanguageType();
        string instanceId = Guid.NewGuid().ToString();

        using HttpResponseMessage resumeResponse = await HttpHelpers.InvokeHttpTrigger("ResumeInstance", $"?instanceId={instanceId}");
        Assert.Equal(HttpStatusCode.BadRequest, resumeResponse.StatusCode);

        string? responseMessage = await resumeResponse.Content.ReadAsStringAsync();
        Assert.NotNull(responseMessage);

        if (languageType == LanguageType.DotnetIsolated)
        {
            Assert.Contains("Status(StatusCode=\"NotFound\"", responseMessage);
        }
        else if (languageType == LanguageType.Java)
        {
            Assert.Contains("NOT_FOUND: ArgumentException: No instance", responseMessage);
        }

        Assert.Contains(fixture.functionLanguageLocalizer.GetLocalizedStringValue("ResumeInvalidInstance.FailureMessage", instanceId), responseMessage);
    }


    [Fact]
    public async Task SuspendResumeCompletedOrchestration_ShouldFail()
    {
        LanguageType languageType = this.fixture.functionLanguageLocalizer.GetLanguageType();

        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=HelloCities");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);


        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 15);

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
                await this.AssertRequestFailsAsync(suspendResponse, fixture.functionLanguageLocalizer.GetLocalizedStringValue("SuspendCompletedInstance.FailureMessage", instanceId));

                await this.AssertRequestFailsAsync(resumeResponse, fixture.functionLanguageLocalizer.GetLocalizedStringValue("ResumeCompletedInstance.FailureMessage", instanceId));
            }

            // PowerShell, Python, Node all use the HTTP suspend/resume APIs, which return 410 (Gone) and do not log
            // when the instance is completed
            if (languageType != LanguageType.PowerShell && languageType != LanguageType.Python && languageType != LanguageType.Node)
            {
                await this.fixture.TestLogs.AssertLogExistsAsync(
                    x => x.Contains("Cannot suspend orchestration instance in the Completed state.") && x.Contains(instanceId),
                    $"Expected 'Cannot suspend orchestration instance in the Completed state.' log for instance '{instanceId}' was not found.");

                await this.fixture.TestLogs.AssertLogExistsAsync(
                    x => x.Contains("Cannot resume orchestration instance in the Completed state.") && x.Contains(instanceId),
                    $"Expected 'Cannot resume orchestration instance in the Completed state.' log for instance '{instanceId}' was not found.");
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
        Assert.Contains(expectedErrorMessage, responseMessage);
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
