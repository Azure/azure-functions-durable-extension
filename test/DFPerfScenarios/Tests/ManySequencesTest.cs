// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace DFPerfScenarios.Tests
{
    public static class ManySequencesTest
    {
        [FunctionName(nameof(StartManySequences))]
        public static async Task<IActionResult> StartManySequences(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            [DurableClient] IDurableClient starter,
            ILogger log)
        {
            if (!int.TryParse(req.Query["count"], out int count) || count < 1)
            {
                return new BadRequestObjectResult("A 'count' query string parameter is required and it must contain a positive number.");
            }

            string orchestratorName = nameof(ManySequencesOrchestrator);
            string instanceId = $"{orchestratorName}-{DateTime.UtcNow:yyyyMMdd-hhmmss}";
            await starter.StartNewAsync(orchestratorName, instanceId, count);

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName(nameof(ManySequencesOrchestrator))]
        public static async Task ManySequencesOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            int count = context.GetInput<int>();

            context.SetCustomStatus(
                $"Scheduling {count:N0} {nameof(Common.HelloSequence)} sequences...");

            var instances = new List<Task>(capacity: count);
            for (int i = 0; i < count; i++)
            {
                string instanceId = $"{context.InstanceId}-sub{i:X16}";
                instances.Add(context.CallSubOrchestratorAsync(nameof(Common.HelloSequence), input: null));

                // Checkpoint every 1K instances to avoid an overly long persistence pauses
                if (i > 0 && i % 1000 == 0)
                {
                    await context.CreateTimer(context.CurrentUtcDateTime, CancellationToken.None);
                }
            }

            context.SetCustomStatus(
                $"All {count:N0} {nameof(Common.HelloSequence)} sequences were scheduled successfully. Waiting for completion...");

            try
            {
                // wait for all instances to complete
                await Task.WhenAll(instances);
            }
            catch
            {
                // ignore failures
            }

            int succeeded = instances.Count(i => i.IsCompletedSuccessfully);
            int failed = instances.Count(i => i.IsFaulted);

            context.SetCustomStatus(
                $"All {count:N0} {nameof(Common.HelloSequence)} sequences have completed. Succeeded: **{succeeded}**. Failed: **{failed}**.");
        }
    }
}