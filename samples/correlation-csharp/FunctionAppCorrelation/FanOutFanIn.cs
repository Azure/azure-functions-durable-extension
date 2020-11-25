// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace FunctionAppCorrelation
{
    public class FanOutFanIn
    {
        [FunctionName(nameof(FanOutFanInOrchestrator))]
        public static async Task<string[]> FanOutFanInOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var tasks = new Task<string>[3];
            tasks[0] = context.CallActivityAsync<string>(nameof(FanOutFanIn_Hello), "Tokyo");
            tasks[1] = context.CallActivityAsync<string>(nameof(FanOutFanIn_Hello), "Seattle");
            tasks[2] = context.CallActivityAsync<string>(nameof(FanOutFanIn_Hello), "London");
            return await Task.WhenAll(tasks);
        }

        [FunctionName(nameof(FanOutFanIn_Hello))]
        public static string FanOutFanIn_Hello([ActivityTrigger] string name, ILogger log)
        {
            log.LogInformation($"Saying hello to {name} at the same time.");
            return $"Hello {name}!";
        }

        [FunctionName(nameof(HttpStart_FanOutFanIn))]
        public async Task<IActionResult> HttpStart_FanOutFanIn(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")]
            HttpRequest req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            string instanceId = await starter.StartNewAsync(nameof(FanOutFanInOrchestrator), null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}
