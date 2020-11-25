// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace FunctionAppCorrelation
{
    public class HttpEndpoints
    {
        [FunctionName(nameof(CheckSiteAvailable))]
        public async Task<string> CheckSiteAvailable(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            Uri uri = context.GetInput<Uri>();

            // Makes an HTTP GET request to the specified endpoint
            DurableHttpResponse response =
                await context.CallHttpAsync(HttpMethod.Get, uri);

            if ((int)response.StatusCode >= 400)
            {
                throw new Exception($"HttpEndpoint can not found: {uri} statusCode: {response.StatusCode} body: {response.Content}");
            }

            return response.Content;
        }

        [FunctionName(nameof(HttpEndpoint))]
        public Task<IActionResult> HttpEndpoint(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")]
            HttpRequest req,
            ILogger log)
        {
            log.LogInformation($"HttpEndpoint is called.'.");
            return Task.FromResult<IActionResult>(new OkObjectResult(new HealthCheck() { Status = "Healthy" }));
        }

        [FunctionName(nameof(HttpStart_HttpEndpoints))]
        public async Task<IActionResult> HttpStart_HttpEndpoints(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")]
            HttpRequest req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            var hostname = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");
            var uri = hostname != null && !hostname.Contains("localhost") ? $"https://{hostname}" : "http://localhost:7071";
            string instanceId =
                await starter.StartNewAsync(nameof(this.CheckSiteAvailable), new Uri($"{uri}/api/{nameof(this.HttpEndpoint)}"));
            log.LogInformation($"Started HttpEndpoints orchestration with ID = '{instanceId}'.");
            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        public class HealthCheck
        {
            public string Status { get; set; }
        }
    }
}
