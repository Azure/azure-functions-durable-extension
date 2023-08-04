using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace DFPerfScenarios
{
    public static class SubOrchestrationFanOutFanIn
    {
        [FunctionName("StartSubOrchestrationFanOutFanIn")]
        public static async Task<HttpResponseMessage> Start(
            [HttpTrigger(AuthorizationLevel.Function, methods: "post", Route = "StartSubOrchestrationFanOutFanIn")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            int num = await req.Content.ReadAsAsync<int>();
            if (num <= 0)
            {
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, "A positive integer was expected in the request body.");
            }
            string instanceId = await starter.StartNewAsync<object>("FanOutFanInOrchestration", num);
            log.LogWarning("Started FanOutFanInOrchestration orchestration with ID = '" + instanceId + "'.");
            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("FanOutFanInOrchestration")]
        public static async Task FanOutFanInOrchestration([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            Task[] array = new Task[context.GetInput<int>()];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = context.CallSubOrchestratorAsync("NoOpOrchestration", (object)i);
            }
            await Task.WhenAll(array);
        }

        [FunctionName("NoOpOrchestration")]
        public static int NoOpOrchestration([OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            return ctx.GetInput<int>();
        }
    }
}
