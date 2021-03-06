using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace DFPerfScenarios
{
	public static class FanOutFanIn
	{
		[FunctionName("StartFanOutFanIn")]
		public static async Task<HttpResponseMessage> Start(
            [HttpTrigger(AuthorizationLevel.Function, methods: "post", Route = "StartFanOutFanIn")] HttpRequestMessage req,
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

			string instanceId = await starter.StartNewAsync<object>("FanOutFanIn", count);
			log.LogInformation("Started FanOutFanIn orchestration with ID = '" + instanceId + "'.");
			return starter.CreateCheckStatusResponse(req, instanceId);
		}

		[FunctionName("FanOutFanIn")]
		public static async Task<TimeSpan> FanOutFanInOrchestration([OrchestrationTrigger] IDurableOrchestrationContext context)
		{
			Stopwatch sw = new Stopwatch();
			if (!context.IsReplaying)
			{
				sw = Stopwatch.StartNew();
			}

            int count = context.GetInput<int>();

            Task[] tasks = new Task[count];
            for (int i = 0; i < tasks.Length; i++)
            {
	            tasks[i] = context.CallActivityAsync<string>("SayHello", i.ToString("0000"));
            }

            await Task.WhenAll(tasks);
			return sw.Elapsed;
		}
	}
}
