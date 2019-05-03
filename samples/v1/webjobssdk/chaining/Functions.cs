// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace DFWebJobsSample
{
    public static class Functions
    {
        public static async Task CronJob(
            [TimerTrigger("0 */2 * * * *")] TimerInfo timer,
            [OrchestrationClient] DurableOrchestrationClient client,
            ILogger logger)
        {
            logger.LogInformation("Cron job fired!");

            string instanceId = await client.StartNewAsync(nameof(HelloSequence), input: null);
            logger.LogInformation($"Started new instance with ID = {instanceId}.");

            DurableOrchestrationStatus status;
            while (true)
            {
                status = await client.GetStatusAsync(instanceId);
                logger.LogInformation($"Status: {status.RuntimeStatus}, Last update: {status.LastUpdatedTime}.");

                if (status.RuntimeStatus == OrchestrationRuntimeStatus.Completed ||
                    status.RuntimeStatus == OrchestrationRuntimeStatus.Failed ||
                    status.RuntimeStatus == OrchestrationRuntimeStatus.Terminated)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(2));
            }

            logger.LogInformation($"Output: {status.Output}");
        }

        public static async Task<List<string>> HelloSequence(
            [OrchestrationTrigger] DurableOrchestrationContextBase context)
        {
            var outputs = new List<string>();

            outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "Tokyo"));
            outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "Seattle"));
            outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "London"));

            // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
            return outputs;
        }

        public static string SayHello([ActivityTrigger] string name, ILogger logger)
        {
            string greeting = $"Hello {name}!";
            logger.LogInformation(greeting);
            Thread.Sleep(5000);
            return greeting;
        }
    }
}
