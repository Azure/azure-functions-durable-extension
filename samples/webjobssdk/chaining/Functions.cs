using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace DFWebJobsSample
{
    public static class Functions
    {
        public static async Task CronJob(
            [TimerTrigger("0 */2 * * * *")] TimerInfo timer,
            [OrchestrationClient] DurableOrchestrationClient client,
            TraceWriter log)
        {
            Console.WriteLine("Cron job fired!");

            string instanceId = await client.StartNewAsync(nameof(HelloSequence), input: null);
            Console.WriteLine($"Started new instance with ID = {instanceId}.");

            DurableOrchestrationStatus status;
            while (true)
            {
                status = await client.GetStatusAsync(instanceId);
                log.Warning($"Status: {status.RuntimeStatus}, Last update: {status.LastUpdatedTime}.");

                if (status.RuntimeStatus == OrchestrationRuntimeStatus.Completed ||
                    status.RuntimeStatus == OrchestrationRuntimeStatus.Failed ||
                    status.RuntimeStatus == OrchestrationRuntimeStatus.Terminated)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(2));
            }

            log.Warning($"Output: {status.Output}");
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

        public static string SayHello([ActivityTrigger] string name, TraceWriter log)
        {
            string greeting = $"Hello {name}!";
            log.Warning(greeting);
            Thread.Sleep(10000);
            return greeting;
        }
    }
}
