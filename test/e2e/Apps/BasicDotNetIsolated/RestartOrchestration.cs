// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Microsoft.Azure.Durable.Tests.E2E;

public static class RestartOrchestration
{
    [Function(nameof(RestartOrchestrator))]
    public static string RestartOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        string? input = context.GetInput<string>();
        return "Hello " + input;
    }

    [Function("RestartOrchestration_HttpStart")]
    public static async Task<HttpResponseData> HttpStartRestartOrchestration(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(RestartOrchestrator), input: "World");
        return await client.CreateCheckStatusResponseAsync(req, instanceId);
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
        string newInstanceId = await client.RestartAsync(data.InstanceId,data.RestartWithNewInstanceId);
        
        return await client.CreateCheckStatusResponseAsync(req, newInstanceId);
    }
} 