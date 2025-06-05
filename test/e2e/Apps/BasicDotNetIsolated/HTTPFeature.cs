// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json.Nodes;

namespace Microsoft.Azure.Durable.Tests.E2E;

public static class HTTPFeature
{
    // Orchestration that takes 2 minutes to complete and will return "Long-running orchestration completed." if completed.
    [Function(nameof(HTTPLongRunningOrchestrator))]
    public static async Task<string> HTTPLongRunningOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(HTTPLongRunningOrchestrator));

        await context.CreateTimer(TimeSpan.FromMinutes(2),CancellationToken.None);

        return "Long-running orchestration completed.";
    }

    // Http trigger that starts the HTTPLongRunningOrchestrator.
    [Function("HttpStart_HTTPLongRunningOrchestrator")]
    public static async Task<HttpResponseData> StartHTTPLongRunningOrchestrator(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,   
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("HttpStart_HTTPLongRunningOrchestrator");

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(HTTPLongRunningOrchestrator));

        logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);
        var response = await client.CreateCheckStatusResponseAsync(req, instanceId);
        return response;
    }

    // Orchestration that will calls the HTTP trigger to start the HTTPLongRunningOrchestrator.
    // It should automatically poll the 202 response until it receive a non-202 response, which should be when the HTTPLongRunningOrchestrator is completed.
    // And this orchestration will return the result of HTTPLongRunningOrchestrator that should contains "Long-running orchestration completed."
    [Function(nameof(HTTPPollingOrchestrator))]
    public static async Task<DurableHttpResponse> HTTPPollingOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(HTTPPollingOrchestrator));
        Uri? url = context.GetInput<Uri>();
        var response = await context.CallHttpAsync(HttpMethod.Get, url!);   
        return response;
    }

    // Http trigger that starts the HTTPPollingOrchestrator.
    [Function("HttpStart_HTTPPollingOrchestrator")]
    public static async Task<HttpResponseData> StartHTTPPollingOrchestrator(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("HttpStart_HTTPPollingOrchestrator");
        
        var builder = new UriBuilder(req.Url)
        {
            Path = "/api/HttpStart_HTTPLongRunningOrchestrator"
        };

        Uri targetUri = builder.Uri;

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(HTTPPollingOrchestrator),targetUri);

        logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

        var response = await client.CreateCheckStatusResponseAsync(req, instanceId);
        return response;
    }
}