using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using static System.Net.Mime.MediaTypeNames;
using Microsoft.AspNetCore.Mvc;

namespace DFPerfScenarios
{
    public static class ManyInstancesTest
    {
        [FunctionName("StartManyInstances")]
        public static async Task<IActionResult> Start(
            [HttpTrigger(AuthorizationLevel.Function, methods: "post", Route = "StartManyInstances")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            Guid newGuid = Guid.NewGuid();
            int num = await HttpContentExtensions.ReadAsAsync<int>(req.Content);
            if (num <= 0)
            {
                return new BadRequestObjectResult("Request body expects an instance count.");
            }

            log.LogWarning($"Starting {num} instances...");
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 100
            };

            try
            {
                Parallel.For(0, num, parallelOptions, delegate (int i) {
                    string text = $"instance_{newGuid}_{i:000000}";
                    starter.StartNewAsync<object>("HelloSequence", text, null).GetAwaiter().GetResult();            });
            }
            catch (Exception ex)
            {
                var exception = new Exception($"Error for GUID ${newGuid}", ex); ;
                return new BadRequestObjectResult(exception);
            }

            return new OkObjectResult($"Created {num} instances successfully!");
        }
    }
}
