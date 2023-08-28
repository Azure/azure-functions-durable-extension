using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace AddingConfigDistTracingTesting
{
    public static class DurableFunctionsPatterns
    {
        [FunctionName("FunctionChaining")]
        public static async Task<List<string>> FunctionChaining(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var outputs = new List<string>();

            outputs.Add(await context.CallActivityAsync<string>(nameof(SayHelloActivity), "Tokyo"));
            outputs.Add(await context.CallActivityAsync<string>(nameof(SayHelloActivity), "Seattle"));
            outputs.Add(await context.CallActivityAsync<string>(nameof(SayHelloActivity), "London"));

            return outputs;
        }

        [FunctionName("FanOutFanIn")]
        public static async Task<string[]> FanOutFanIn(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var tasks = new List<Task<string>>();

            tasks.Add(context.CallActivityAsync<string>(nameof(SayHelloActivity), "Tokyo"));
            tasks.Add(context.CallActivityAsync<string>(nameof(SayHelloActivity), "Seattle"));
            tasks.Add(context.CallActivityAsync<string>(nameof(SayHelloActivity), "London"));

            return await Task.WhenAll(tasks);
        }

        [FunctionName("Monitoring")]
        public static async Task<string> Monitoring(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            int pollingInterval = 10;
            DateTime expiryTime = context.CurrentUtcDateTime.AddMinutes(2);
            int numTries = 1;
            string completedString = "Completed";

            while (context.CurrentUtcDateTime < expiryTime)
            {
                context.SetCustomStatus($"Tried getting the status {numTries} times.");
                var jobStatus = await context.CallActivityAsync<string>(nameof(GetStatus), numTries);
                if (jobStatus == completedString)
                {
                    return completedString;
                }

                var nextCheck = context.CurrentUtcDateTime.AddSeconds(pollingInterval);
                await context.CreateTimer(nextCheck, CancellationToken.None);
                numTries++;
            }

            return "";
        }

        // Send an event called "Approval Event"
        [FunctionName("HumanInteraction")]
        public static async Task<string> HumanInteraction(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            using (var timeoutCts = new CancellationTokenSource())
            {
                DateTime dueTime = context.CurrentUtcDateTime.AddMinutes(5);
                Task durableTimeout = context.CreateTimer(dueTime, timeoutCts.Token);

                Task<bool> approvalEvent = context.WaitForExternalEvent<bool>("ApprovalEvent");
                if (approvalEvent == await Task.WhenAny(approvalEvent, durableTimeout))
                {
                    timeoutCts.Cancel();
                    return "Process approval";
                }
                else
                {
                    return "escalate";
                }
            }
        }

        [FunctionName(nameof(SayHelloActivity))]
        public static string SayHelloActivity([ActivityTrigger] string name, ILogger log)
        {
            log.LogInformation("Saying hello to {name}.", name);
            return $"Hello {name}!";
        }

        [FunctionName(nameof(GetStatus))]
        public static string GetStatus([ActivityTrigger] int numTries, ILogger log)
        {
            if (numTries == 3)
            {
                return "Completed";
            }

            return "In Progress";
        }

        [FunctionName("DurableFunctionsPatterns_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "orchestrators/{functionName}")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            string functionName,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync(functionName, null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}