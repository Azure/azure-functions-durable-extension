using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace TimeoutTests
{
    public static partial class TimeoutTests
    {
        [FunctionName(nameof(OrchestrationTimeout))]
        public static async Task<string> OrchestrationTimeout(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            try
            {
                var result = await context.CallSubOrchestratorAsync<string>("SlowOrchestration", null);
                return "Test failed: no exception thrown";
            }
            catch (Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionFailedException e)
                when (e.InnerException is Microsoft.Azure.WebJobs.Host.FunctionTimeoutException)
            {
                return "Test succeeded";
            }
            catch (Microsoft.Azure.WebJobs.Host.FunctionTimeoutException)
            {
                return "Test succeeded";
            }
            catch (Exception e)
            {
                return $"Test failed: wrong exception thrown: {e}";
            }
        }

        [FunctionName(nameof(SlowOrchestration))]
        public static Task<string> SlowOrchestration(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger logger)
        {
            int seconds = 180;
            logger.LogWarning($"{context.InstanceId} starting slow orchestration duration={seconds}s");
#pragma warning disable DF0103 // Thread.Sleep and Task.Delay calls are not allowed inside an orchestrator function.
            System.Threading.Thread.Sleep(seconds * 1000); // does not complete within the 00:02:00 timeout setting
#pragma warning restore DF0103 // Thread.Sleep and Task.Delay calls are not allowed inside an orchestrator function.
            return Task.FromResult("Done waiting");
        }
    }
}
