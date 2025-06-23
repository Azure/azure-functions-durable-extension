// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using System.Text.Json.Nodes;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Http;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Durable.Tests.E2E;

public static class HttpFeature
{
    // Orchestration that takes 1 minutes to complete and will return "Long-running orchestration completed." if completed.
    [Function(nameof(HttpLongRunningOrchestrator))]
    public static async Task<string> HttpLongRunningOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(HttpLongRunningOrchestrator));

        await context.CreateTimer(TimeSpan.FromMinutes(1),CancellationToken.None);

        return "Long-running orchestration completed.";
    }

    // Http trigger that starts the HttpLongRunningOrchestrator.
    [Function("HttpStart_HttpLongRunningOrchestrator")]
    public static async Task<HttpResponseData> StartHttpLongRunningOrchestrator(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,   
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("HttpStart_HttpLongRunningOrchestrator");

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(HttpLongRunningOrchestrator));

        logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);
        var response = await client.CreateCheckStatusResponseAsync(req, instanceId);
        return response;
    }

    // Orchestration that will calls the Http trigger to start the HttpLongRunningOrchestrator.
    // It should automatically poll the 202 response until it receive a non-202 response, which should be when the HttpLongRunningOrchestrator is completed.
    // And this orchestration will return the result of HttpLongRunningOrchestrator that should contains "Long-running orchestration completed."
    [Function(nameof(HttpPollingOrchestrator))]
    public static async Task<DurableHttpResponse> HttpPollingOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(HttpPollingOrchestrator));
        Uri? url = context.GetInput<Uri>();
        var response = await context.CallHttpAsync(
            HttpMethod.Get,
            url!,
            content: null,
            retryOptions: null,
            asynchronousPatternEnabled: true);   
        return response;
    }

    // Http trigger that starts the HttpPollingOrchestrator.
    [Function("HttpStart_HttpPollingOrchestrator")]
    public static async Task<HttpResponseData> StartHttpPollingOrchestrator(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("HttpStart_HttpPollingOrchestrator");
        
        var builder = new UriBuilder(req.Url)
        {
            Path = "/api/HttpStart_HttpLongRunningOrchestrator"
        };

        Uri targetUri = builder.Uri;

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(HttpPollingOrchestrator),targetUri);

        logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

        var response = await client.CreateCheckStatusResponseAsync(req, instanceId);
        return response;
    }

    // Orchestration that Call HTTP with token credential.
    [Function(nameof(HttpWithTokenCredentialOrchestrator))]
    public static async Task<string> HttpWithTokenCredentialOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(HttpWithTokenCredentialOrchestrator));
        
        // Create a token source using managed identity for Azure Management API
        var tokenSource = new ManagedIdentityTokenSource("https://management.core.windows.net/.default");
        
        // For testing purposes, we'll use a mock URL that doesn't actually require authentication
        // In a real scenario, this would be an Azure service endpoint
        var testUrl = new Uri("https://httpbin.org/get");
        
        try
        {
            // Make an HTTP call with token authentication
            var response = await context.CallHttpAsync(
                HttpMethod.Get,
                testUrl,
                content: null,
                retryOptions: null,
                asynchronousPatternEnabled: false,
                tokenSource: tokenSource,
                timeout: null);

            logger.LogInformation("HTTP call completed with status code: {StatusCode}", response.StatusCode);
            
            return $"HTTP call with managed identity completed successfully. Status: {response.StatusCode}";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "HTTP call with managed identity failed");
            return $"HTTP call with managed identity failed: {ex.Message}";
        }
    }

    // Http trigger that starts the HttpWithTokenCredentialOrchestrator.
    [Function("HttpStart_HttpWithTokenCredentialOrchestrator")]
    public static async Task<HttpResponseData> StartHttpWithTokenCredentialOrchestrator(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("HttpStart_HttpWithTokenCredentialOrchestrator");

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(HttpWithTokenCredentialOrchestrator));

        logger.LogInformation("Started managed identity orchestration with ID = '{instanceId}'.", instanceId);

        var response = await client.CreateCheckStatusResponseAsync(req, instanceId);
        return response;
    }
}