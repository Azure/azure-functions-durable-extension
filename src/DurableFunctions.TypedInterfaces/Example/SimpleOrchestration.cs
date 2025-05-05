// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.TypedInterfaces;
using Microsoft.Extensions.Logging;

namespace WebJobs.Extensions.DurableTask.CodeGen.Example
{
    public class SimpleOrchestration
    {
        [FunctionName("SimpleOrchestrationHttp")]
        public static async Task<IActionResult> HttpStart_SimpleOrchestration(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req,
            // This project intentionally violates this analyzer
#pragma warning disable DF0203
            [DurableClient] ITypedDurableClient client,
#pragma warning restore DF0203
            ILogger log)
        {
            string instanceId = await client.Orchestrations.StartSimpleOrchestration(null);
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
            return client.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("SimpleOrchestration")]
        public static async Task<List<string>> SimpleOrchestrator(
            // This project intentionally violates this analyzer
#pragma warning disable DF0201
            [OrchestrationTrigger] ITypedDurableOrchestrationContext context)
#pragma warning restore DF0201
        {
            var outputs = new List<string>();

            // Replace "hello" with the name of your Durable Activity Function.
            outputs.Add(await context.Activities.SayHello("Tokyo"));
            outputs.Add(await context.Activities.SayHello("Seattle"));
            outputs.Add(await context.Activities.SayHello("London"));

            return outputs;
        }

        [FunctionName("SayHello")]
        public static string SayHello([ActivityTrigger] IDurableActivityContext context, ILogger log)
        {
            var name = context.GetInput<string>();

            log.LogInformation($"Saying hello to {name}.");
            return $"Hello {name}!";
        }

    }
}
