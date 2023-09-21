using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading.Tasks;

namespace DFPerfScenarios
{
    public static class ExponentialFanOutV1
    {
        [FunctionName("ExponentialFanOutV1")]
        public static async Task ExponentialFanOut([OrchestrationTrigger] IDurableOrchestrationContext ctx, ILogger log)
        {
            int generationsRemaining = ctx.GetInput<int>();
            if (generationsRemaining > 0)
            {
                Task[] array = new Task[10];
                for (int i = 0; i < 10; i++)
                {
                    array[i] = ctx.CallActivityAsync("StartNewGeneration", generationsRemaining);
                }

                await Task.WhenAll(array);
            }
            log.LogInformation($"Completed generation ${generationsRemaining}");
        }

        [FunctionName("StartNewGeneration")]
        public static async Task StartNewGeneration([ActivityTrigger] int generationCount, [DurableClient] IDurableOrchestrationClient client)
        {
            await client.StartNewAsync("ExponentialFanOutV1", (object)(generationCount - 1));
        }

        [FunctionName("StartExponential")]
        public static async Task<HttpResponseMessage> StartExponential(
            [HttpTrigger(AuthorizationLevel.Function, methods: "post", Route = "StartExponential")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            int input = await req.Content.ReadAsAsync<int>();
            string text = await starter.StartNewAsync<object>("ExponentialFanOutV1", input);
            log.LogInformation($"Started ExponentialFanOutV1 orchestration with ID = '{text}'.");
            return starter.CreateCheckStatusResponse(req, text);
        }
    }
}
