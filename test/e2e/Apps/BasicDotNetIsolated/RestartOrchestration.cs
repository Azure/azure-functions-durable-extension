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

    // Orchestration that waits on a long-running timer.
    // Used for testing restart of an orchestration that has not yet completed.
    [Function(nameof(LongOrchestrator))]
    public static async Task<List<string>> LongOrchestrator(
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

    // HTTP-triggered function that starts a new durable orchestration instance.
    [Function("RestartOrchestration_HttpStart")]
    public static async Task<HttpResponseData> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "RestarttOrchestration_HttpStart/{orchestratorName}")] HttpRequestData req,
        string orchestratorName,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("Function1_HttpStart");

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            orchestratorName);

        logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);
        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }

    // HTTP-triggered function that restarts a durable orchestration instance using the provided instance ID and restart options. 
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

    // HTTP-triggered function that restarts a durable orchestration instance with comprehensive error handling.
    // Returns the new instance ID on success, or returns the error message with appropriate HTTP status codes on failure. 
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
        catch (Exception ex)
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            response.Headers.Add("Content-Type", "application/json");

            string message = ex.Message;
            
            await response.WriteStringAsync(message);
            return response;
        }
    }
}
