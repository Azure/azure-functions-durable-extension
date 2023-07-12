using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace DFPerfScenarios
{
    public static class ManyInstancesTest
    {
        [FunctionName("StartManyInstances")]
        public static async Task<HttpResponseMessage> Start(
            [HttpTrigger(AuthorizationLevel.Function, methods: "post", Route = "StartManyInstances")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            int num = await HttpContentExtensions.ReadAsAsync<int>(req.Content);
            if (num <= 0)
            {
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, "Request body expects an instance count.");
            }

            log.LogWarning($"Starting {num} instances...");
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 100
            };

            Parallel.For(0, num, parallelOptions, delegate (int i) {
                string text = $"instance_{i:000000}";
                starter.StartNewAsync<object>("HelloSequence", text, null).GetAwaiter().GetResult();
            });

            log.LogWarning($"Created {num} instances successfully!");
            return req.CreateResponse();
        }
    }
}
