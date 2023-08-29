using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
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
            int num = await HttpContentExtensions.ReadAsAsync<int>(req.Content);
            if (num <= 0)
            {
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, "A positive integer was expected in the request body.");
            }

            string instanceId = await starter.StartNewAsync<object>("FanOutFanIn", num);
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
