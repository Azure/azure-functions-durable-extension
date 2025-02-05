// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Grpc.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Durable.Tests.E2E
{
    public static class LongRunningOrchestration
    {
        [Function(nameof(LongRunningOrchestrator))]
        public static async Task<List<string>> LongRunningOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(HelloCities));
            logger.LogInformation("Starting long-running orchestration.");
            var outputs = new List<string>();

            // Call our fake activity 100,000 times to simulate an orchestration that might run for >= 10,000s (2.7 hours)
            for (int i = 0; i < 100000; i++) 
            {
                outputs.Add(await context.CallActivityAsync<string>(nameof(SimulatedWorkActivity), 100));
            }

            return outputs;
        }

        [Function(nameof(SimulatedWorkActivity))]
        public static string SimulatedWorkActivity([ActivityTrigger]int sleepMs, FunctionContext executionContext)
        {
            // Sleep the provided number of ms to simulate a long-running activity operation
            ILogger logger = executionContext.GetLogger("SimulatedWorkActivity");
            logger.LogInformation("Sleeping for {sleepMs}ms.", sleepMs);
            Thread.Sleep(sleepMs);
            return $"Slept for {sleepMs}ms.";
        }

        [Function("LongOrchestrator_HttpStart")]
        public static async Task<HttpResponseData> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("LongOrchestrator_HttpStart");

            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(LongRunningOrchestrator));

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);
            
            return await client.CreateCheckStatusResponseAsync(req, instanceId);
        }

        [Function("TerminateInstance")]
        public static async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            string instanceId)
        {
            string reason = "Long-running orchestration was terminated early.";
            try 
            {
                await client.TerminateInstanceAsync(instanceId, reason);
                return req.CreateResponse(HttpStatusCode.OK);
            }
            catch (RpcException ex) 
            {
                var response = req.CreateResponse(HttpStatusCode.BadRequest);
                response.Headers.Add("Content-Type", "text/plain");
                await response.WriteStringAsync(ex.Message);
                return response;
            }
        }
    }
}
