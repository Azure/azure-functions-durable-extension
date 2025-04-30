// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Grpc.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Microsoft.Azure.Durable.Tests.E2E;

public static class ExternalEventOrchestration
{
    [Function("ExternalEventOrchestrator_HttpStart")]
    public static async Task<HttpResponseData> ExternalEventOrchestrator_HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("ExternalEventOrchestrator_HttpStart");
        
        var option = new StartOrchestrationOptions(InstanceId : "ExternalEventTest");
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(ExternalEventOrchestrator), option);

        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }

    [Function(nameof(ExternalEventOrchestrator))]
    public static async Task<string> ExternalEventOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        bool approval = await context.WaitForExternalEvent<bool>("Approval", CancellationToken.None);

        return "Orchestrator Finished!";
    }

    [Function("NotValidInstanceId_HttpStart")]
    public static async Task<HttpResponseData> NotValidInstanceId_HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "posst")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        var response = req.CreateResponse();

        try
        {
            await client.RaiseEventAsync("", "Approval", true);
            response.StatusCode = HttpStatusCode.OK;
            await response.WriteStringAsync("External event sent.");
        }
        catch (Exception ex)
        {
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync($"Unhandled error of type {ex.GetType().Name}: {ex.Message}");
        }

        return response;
    }
    
    [Function("SendExternalEvent_HttpStart")]
    public static async Task<HttpResponseData> SendExternalEvent_HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        var response = req.CreateResponse();

        try
        {
            await client.RaiseEventAsync("ExternalEventTest", "Approval", true);
            response.StatusCode = HttpStatusCode.OK;
            await response.WriteStringAsync("External event sent.");
        }
        catch (RpcException ex)
        {
            response.StatusCode = HttpStatusCode.BadRequest;
            await response.WriteStringAsync($"gRPC error: {ex.StatusCode} - {ex.Message}");
        }

        return response;
    }
}
