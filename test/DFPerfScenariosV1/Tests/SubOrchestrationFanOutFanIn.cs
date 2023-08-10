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
			if (!int.TryParse(req.GetQueryNameValuePairs().First(q => q.Key == "count").Value, out int count) || count < 1)
			{
				return new HttpResponseMessage
				{
					StatusCode = HttpStatusCode.BadRequest,
					ReasonPhrase = "A 'count' query string parameter is required and it must contain a positive number."
				};
			}

			string instanceId = await starter.StartNewAsync<object>("FanOutFanInOrchestration", count);
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
