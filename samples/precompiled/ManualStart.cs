using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace VSSample
{
    public static class ManualStart
    {
        [FunctionName("ManualStart")]
        public static async Task Run(
            [HttpTrigger] string functionName,
            [OrchestrationClient] DurableOrchestrationClient starter,
            ILogger log)
        {
            log.LogInformation($"Starting orchestration named: {functionName}");
            string instanceId = await starter.StartNewAsync(functionName, null);
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
        }
    }
}