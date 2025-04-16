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
    [Function(nameof(HTTPLongRunningOrchestrator))]
    public static async Task<List<string>> HTTPLongRunningOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(HTTPLongRunningOrchestrator));
        var outputs = new List<string>();

        outputs.Add(await context.CallActivityAsync<string>(nameof(HTTPSayHello), "Tokyo"));
        await context.CreateTimer(TimeSpan.FromMinutes(2),CancellationToken.None);

        return outputs;
    }


    [Function(nameof(HTTPSayHello))]
    public static string HTTPSayHello(
        [ActivityTrigger] string name, FunctionContext executionContext)
    {
        return $"Hello {name}!";
    }

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

    [Function(nameof(HTTPPollingOrchestrator))]
    public static async Task<DurableHttpResponse> HTTPPollingOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(HTTPPollingOrchestrator));
        Uri url = context.GetInput<Uri>();
        var response = await context.CallHttpAsync(HttpMethod.Get, url);   
        return response;
    }

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