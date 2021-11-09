// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace FunctionAppCorrelation
{
    public class ExternalEvent
    {
        private const string ApprovalEvent1 = "Approval1";
        private const string ApprovalEvent2 = "Approval2";

        private readonly TelemetryClient telemetryClient;

        public ExternalEvent(TelemetryConfiguration telemetryConfiguration)
        {
            this.telemetryClient = new TelemetryClient(telemetryConfiguration);
        }

        [FunctionName(nameof(WaitForEventOrchestrator))]
        public async Task WaitForEventOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var event1 = context.WaitForExternalEvent<string>(ApprovalEvent1);
            var event2 = context.WaitForExternalEvent<string>(ApprovalEvent2);
            await Task.WhenAll(event1, event2);
            await context.CallActivityAsync(nameof(this.CompletedAlert), $"Event1: {event1.Result} Event2: {event2.Result}");
        }

        [FunctionName(nameof(CompletedAlert))]
        public void CompletedAlert(
            [ActivityTrigger] string message)
        {
            // You can use TrackTrace to send a custom correlated trace message to Application Insights
            this.telemetryClient.TrackTrace(message);
        }

        [FunctionName(nameof(ApprovalOne))]
        public async Task<IActionResult> ApprovalOne(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")]
            HttpRequest req,
            [DurableClient] IDurableOrchestrationClient client,
            ILogger log)
        {
            var body = await req.ReadAsStringAsync();
            var payload = JsonConvert.DeserializeObject<Payload>(body);
            if (payload == null)
            {
                return new BadRequestObjectResult("Payload should be json format with message and instanceId e.g. {\"Message\": \"Approved! one\" , \"InstanceId\":\" \"} ");
            }

            await client.RaiseEventAsync(payload.InstanceId, ApprovalEvent1, payload.Message);
            return new OkObjectResult(
                $"Approved: Number:1 InstanceId: {payload.InstanceId} Message: {payload.Message} ");
        }

        [FunctionName(nameof(ApprovalTwo))]
        public async Task<IActionResult> ApprovalTwo(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")]
            HttpRequest req,
            [DurableClient] IDurableOrchestrationClient client,
            ILogger log)
        {
            var body = await req.ReadAsStringAsync();
            var payload = JsonConvert.DeserializeObject<Payload>(body);
            if (payload == null)
            {
                return new BadRequestObjectResult("Payload should be json format with message and instanceId e.g. {\"Message\": \"Approved! two.\" , \"InstanceId\":\" \"} ");
            }

            await client.RaiseEventAsync(payload.InstanceId, ApprovalEvent2, payload.Message);
            return new OkObjectResult(
                $"Approved: Number:1 InstanceId: {payload.InstanceId} Message: {payload.Message} ");
        }

        [FunctionName(nameof(HttpStart_ExternalEvent))]
        public async Task<IActionResult> HttpStart_ExternalEvent(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")]
            HttpRequest req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            string instanceId = await starter.StartNewAsync(nameof(this.WaitForEventOrchestrator), null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        public class Payload
        {
            public string InstanceId { get; set; }

            public string Message { get; set; }
        }
    }
}
