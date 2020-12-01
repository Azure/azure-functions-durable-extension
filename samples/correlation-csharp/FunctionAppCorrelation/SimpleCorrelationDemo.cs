// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using DurableTask.Core;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace FunctionAppCorrelation
{
    public class SimpleCorrelationDemo
    {
        private readonly TelemetryClient telemetryClient;
        private readonly HttpClient httpClient;

        public SimpleCorrelationDemo(TelemetryConfiguration telemetryConfiguration, HttpClient client)
        {
            this.telemetryClient = new TelemetryClient(telemetryConfiguration);
            this.httpClient = client;
        }

        [FunctionName(nameof(Orchestration_W3C))]
        public async Task<List<string>> Orchestration_W3C(
           [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            if (!(CorrelationTraceContext.Current is W3CTraceContext correlationContext))
            {
                throw new InvalidOperationException($"This sample expects a correlation trace context of {nameof(W3CTraceContext)}, but the context isof type {CorrelationTraceContext.Current.GetType()}");
            }

            var trace = new TraceTelemetry(
                $"Activity Id: {correlationContext.TraceParent} ParentSpanId: {correlationContext.ParentSpanId}");
            trace.Context.Operation.Id = correlationContext.TelemetryContextOperationId;
            trace.Context.Operation.ParentId = correlationContext.TelemetryContextOperationParentId;
            this.telemetryClient.Track(trace);

            var outputs = new List<string>
            {
                await context.CallActivityAsync<string>(nameof(this.Hello_W3C), "Tokyo"),
                await context.CallActivityAsync<string>(nameof(this.Hello_W3C), "Seattle"),
                await context.CallActivityAsync<string>(nameof(this.Hello_W3C), "London"),
            };

            // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
            return outputs;
        }

        [FunctionName(nameof(Hello_W3C))]
        public string Hello_W3C([ActivityTrigger] string name, ILogger log)
        {
            // Send Custom Telemetry
            this.telemetryClient.TrackTrace($"Message from Activity: {name}.");

            log.LogInformation($"Saying hello to {name}.");
            return $"Hello {name}!";
        }

        [FunctionName(nameof(HttpStart_With_W3C))]
        public async Task<HttpResponseMessage> HttpStart_With_W3C(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")]HttpRequestMessage req,
            [DurableClient]IDurableOrchestrationClient starter,
            ILogger log)
        {
            var traceparent = req.Headers.GetValues("traceparent").FirstOrDefault(); // HttpCorrelationProtocol uses Request-Id
            var currentActivity = Activity.Current;

            if (string.IsNullOrEmpty(traceparent))
            {
                log.LogInformation("Traceparent can not be empty.");
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, "Traceparent header is required.");
            }

            string instanceId = await starter.StartNewAsync(nameof(this.Orchestration_W3C), null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName(nameof(HttpStart_AnExternalSystem))]
        public async Task<IActionResult> HttpStart_AnExternalSystem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")]
            HttpRequest req,
            ILogger log)
        {
            log.LogInformation($"An external system send a request. ");
            var currentActivity = Activity.Current;
            var hostname = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");
            var uri = $"https://{hostname}"; // Change from https to http with port number for Local Debugging.
            await this.httpClient.GetAsync($"{uri}/api/{nameof(this.HttpStart_With_W3C)}");
            return new OkObjectResult("Telemetry Sent.");
        }
    }
}
