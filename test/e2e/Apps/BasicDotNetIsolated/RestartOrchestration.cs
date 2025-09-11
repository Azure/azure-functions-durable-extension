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

public static class RestartOrchestration
{
    [Function(nameof(SimpleOrchestrator))]
    public static string SimpleOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        string? input = context.GetInput<string>();
        return "Hello " + input;
    }

    [Function(nameof(WaitForLongOrchestrator))]
    public static async Task<List<string>> WaitForLongOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var outputs = new List<string>();

        DateTime fireAt = context.CurrentUtcDateTime.AddMinutes(30);
        await context.CreateTimer(fireAt: fireAt, cancellationToken: CancellationToken.None);
        return outputs;
    }

    public class RestartRequest
    {
        public string InstanceId { get; set; } = string.Empty;
        public bool RestartWithNewInstanceId { get; set; }
    }

    [Function("RestartOrchestration_HttpRestart")]
    public static async Task<HttpResponseData> HttpRestartOrchestration(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        var data = await req.ReadFromJsonAsync<RestartRequest>();
        if (data == null)
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }
        string newInstanceId = await client.RestartAsync(data.InstanceId, data.RestartWithNewInstanceId);
        
        return await client.CreateCheckStatusResponseAsync(req, newInstanceId);
    }

    [Function("RestartOrchestration_HttpRestartWithErrorHandling")]
    public static async Task<HttpResponseData> HttpRestartOrchestrationWithErrorHandling(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        var data = await req.ReadFromJsonAsync<RestartRequest>();
        if (data == null)
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        try
        {
            string newInstanceId = await client.RestartAsync(data.InstanceId, data.RestartWithNewInstanceId);
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync(newInstanceId);
            return response;
        }
        catch (RpcException ex)
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            response.Headers.Add("Content-Type", "application/json");
            
            var errorResponse = new
            {
                StatusCode = ex.StatusCode.ToString(),
                Message = ex.Message
            };
            
            await response.WriteStringAsync(System.Text.Json.JsonSerializer.Serialize(errorResponse));
            return response;
        }
    }
}
