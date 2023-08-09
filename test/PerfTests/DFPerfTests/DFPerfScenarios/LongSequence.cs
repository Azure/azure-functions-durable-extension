using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace DFPerfScenarios
{
    public static class LongSequence
    {
        [FunctionName("RunLongSequence")]
        public static async Task RunLongSequence([OrchestrationTrigger] IDurableOrchestrationContext ctx, ILogger log)
        {
            int iterations = ctx.GetInput<int>();
            if (!ctx.IsReplaying)
            {
                log.LogInformation($"Iterations: {iterations}");
            }
            for (int i = 0; i < iterations; i++)
            {
                if (i % 10 == 0)
                {
                    ctx.SetCustomStatus($"Activity calls: {i}");
                }
                try
                {
                    await ctx.CallActivityAsync<string>("SayHello", i.ToString());
                }
                catch (Exception ex)
                {
                    log.LogError(ex.Message);
                    throw;
                }
            }

            ctx.SetCustomStatus($"Activity calls: {iterations}");
            log.LogInformation("Done!");
        }
    }
}
