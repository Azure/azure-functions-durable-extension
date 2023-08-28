// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace FunctionAppCorrelation
{
    public class SimpleOrchestration
    {
        [FunctionName(nameof(SimpleOrchestrator))]
        public static async Task<List<string>> SimpleOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var outputs = new List<string>();

            // Replace "hello" with the name of your Durable Activity Function.
            outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "Tokyo"));
            outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "Seattle"));
            outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "London"));

            return outputs;
        }

        [FunctionName(nameof(SayHello))]
        public static string SayHello([ActivityTrigger] string name, ILogger log)
        {
            log.LogInformation($"Saying hello to {name}.");
            return $"Hello {name}!";
        }

        [FunctionName(nameof(HttpStart_SimpleOrchestration))]
        public static async Task<IActionResult> HttpStart_SimpleOrchestration(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")]HttpRequest req,
            [DurableClient]IDurableOrchestrationClient starter,
            ILogger log)
        {
            string instanceId = await starter.StartNewAsync(nameof(SimpleOrchestrator), null);
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}
