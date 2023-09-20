using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace DFPerfScenarios
{
    public class Common
    {
        [FunctionName("CatchException")]
        public static async Task<string> CatchException([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            try
            {
                await context.CallActivityWithRetryAsync("ThrowsError", new RetryOptions(TimeSpan.FromSeconds(3), 3), null);
                return "Succeeded initial attempt";
            }
            catch (FunctionFailedException)
            {
                return await context.CallActivityAsync<string>("SayHello", "Tokyo");
            }
        }


        [FunctionName("HelloSequence")]
        public static async Task<List<string>> HelloSequence([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            List<string> outputs = new List<string>();
            outputs.Add(await context.CallActivityAsync<string>("SayHello", "Tokyo"));
            outputs.Add(await context.CallActivityAsync<string>("SayHello", "Seattle"));
            outputs.Add(await context.CallActivityAsync<string>("SayHello", "London"));
            return outputs;
        }

        [FunctionName("HelloActivityDIFailure")]
        public static async Task<List<string>> HelloActivityDIFailure([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            List<string> outputs = new List<string>();
            outputs.Add(await context.CallActivityAsync<string>("FailedActivity", null));
            return outputs;
        }

        [FunctionName("SayHello")]
        public static string SayHello([ActivityTrigger] string name, ILogger log)
        {
            log.LogInformation($"Hello {name}!");
            return "Hello " + name + "!";
        }

        [FunctionName("ThrowsError")]
        public static string ThrowsError([ActivityTrigger] string name, ILogger log)
        {
            throw new System.Exception("");
        }


        [FunctionName("GetContinuationToken")]
        public static async Task<IActionResult> GetContinuationToken([HttpTrigger] HttpRequest req, [DurableClient] IDurableClient client, ILogger log)
        {

            var children = new List<string>();

            var queryCondition = new OrchestrationStatusQueryCondition
            {
                PageSize = 5,
                ShowInput = false,
                ContinuationToken = null
            };

            do
            {
                var status = await client.GetStatusAsync(queryCondition, CancellationToken.None);
                queryCondition.ContinuationToken = status.ContinuationToken;

                children.AddRange(status.DurableOrchestrationState.Select(p => p.InstanceId));
            } while (queryCondition.ContinuationToken != null);


            return new OkObjectResult(children);
        }
    }
}
