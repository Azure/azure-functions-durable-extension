// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class SuspendResumeTests
{
    private readonly FunctionAppFixture _fixture;
    private readonly ITestOutputHelper _output;

    public SuspendResumeTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _fixture.TestLogs.UseTestLogger(testOutputHelper);
        _output = testOutputHelper;
    }


    [Fact]
    public async Task SuspendAndResumeRunningOrchestration_ShouldSucceed()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("LongOrchestrator_HttpStart", "");

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
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("LongOrchestrator_HttpStart", "");

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
            await AssertRequestFailsAsync(resumeResponse);

            // Give some time for Core Tools to write logs out
            Thread.Sleep(500);

            Assert.Contains(_fixture.TestLogs.CoreToolsLogs, x => x.Contains("Cannot suspend orchestration instance in the Suspended state.") &&
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
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("LongOrchestrator_HttpStart", "");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Running", 5);
        try
        {
            using HttpResponseMessage resumeResponse = await HttpHelpers.InvokeHttpTrigger("ResumeInstance", $"?instanceId={instanceId}");
            await AssertRequestFailsAsync(resumeResponse);

            // Give some time for Core Tools to write logs out
            Thread.Sleep(500);

            Assert.Contains(_fixture.TestLogs.CoreToolsLogs, x => x.Contains("Cannot resume orchestration instance in the Running state.") &&
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
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("HelloCities_HttpStart", "");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string instanceId = await DurableHelpers.ParseInstanceIdAsync(response);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 5);
        try
        {
            using HttpResponseMessage suspendResponse = await HttpHelpers.InvokeHttpTrigger("SuspendInstance", $"?instanceId={instanceId}");
            await AssertRequestFailsAsync(suspendResponse);

            using HttpResponseMessage resumeResponse = await HttpHelpers.InvokeHttpTrigger("ResumeInstance", $"?instanceId={instanceId}");
            await AssertRequestFailsAsync(resumeResponse);

            // Give some time for Core Tools to write logs out
            Thread.Sleep(500);


            Assert.Contains(_fixture.TestLogs.CoreToolsLogs, x => x.Contains("Cannot suspend orchestration instance in the Completed state.") &&
                                                                  x.Contains(instanceId));
            Assert.Contains(_fixture.TestLogs.CoreToolsLogs, x => x.Contains("Cannot resume orchestration instance in the Completed state.") &&
                                                                  x.Contains(instanceId));
        }
        finally
        {
            await TryTerminateInstanceAsync(instanceId);
        }
    }

    private static async Task AssertRequestSucceedsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string? responseMessage = await response.Content.ReadAsStringAsync();
        Assert.NotNull(responseMessage);
        Assert.Empty(responseMessage);
    }

    private static async Task AssertRequestFailsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        string? responseMessage = await response.Content.ReadAsStringAsync();
        Assert.NotNull(responseMessage);
        // Unclear error message - see https://github.com/Azure/azure-functions-durable-extension/issues/3027, will update this code when that bug is fixed
        Assert.Equal("Status(StatusCode=\"Unknown\", Detail=\"Exception was thrown by handler.\")", responseMessage);
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
