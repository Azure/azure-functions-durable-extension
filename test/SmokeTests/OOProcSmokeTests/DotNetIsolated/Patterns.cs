using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace DurableTest
{
    public class Patterns
    {
        [Function(nameof(HttpTrigger))]
        public static async Task<HttpResponseData> HttpTrigger(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "orchestrators/{name}")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            string name,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger(nameof(HttpTrigger));
            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(name);
            logger.LogInformation("Created new orchestration with instance ID = {instanceId}", instanceId);
            return client.CreateCheckStatusResponse(req, instanceId);
        }

        [Function(nameof(Chain))]
        public static async Task<string> Chain([OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(Chain));

            async Task<string> Hello(string name)
            {
                logger.LogInformation("Start activity call");
                string result = await context.CallActivityAsync<string>(nameof(SayHello), name);
                logger.LogInformation("End activity call");
                return result;
            }

            List<string> result = new()
            {
                await Hello("Tokyo"),
                await Hello("Seattle"),
                await Hello("London"),
            };

            return string.Join(';', result);
        }

        [Function(nameof(Fan))]
        public static async Task<string> Fan([OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(Fan));

            async Task<string> Hello(string name)
            {
                logger.LogInformation("Start activity call");
                string result = await context.CallActivityAsync<string>(nameof(SayHello), name);
                logger.LogInformation("End activity call");
                return result;
            }

            var tasks = new Task<string>[3]
            {
                Hello("Tokyo"),
                Hello("Seattle"),
                Hello("London"),
            };

            string[] result = await Task.WhenAll(tasks);
            return string.Join(';', result);
        }

        [Function(nameof(Monitor))]
        public static async Task<string> Monitor([OrchestrationTrigger] TaskOrchestrationContext context)
        {
            const string completed = "Completed";
            int pollingInterval = 10;
            DateTime expiryTime = context.CurrentUtcDateTime.AddMinutes(2);
            int numTries = 1;

            while (context.CurrentUtcDateTime < expiryTime)
            {
                context.SetCustomStatus($"Tried getting the status {numTries} times.");
                var jobStatus = await context.CallActivityAsync<string>(nameof(GetStatus), numTries);
                if (jobStatus == completed)
                {
                    return completed;
                }

                var nextCheck = context.CurrentUtcDateTime.AddSeconds(pollingInterval);
                await context.CreateTimer(nextCheck, CancellationToken.None);
                numTries++;
            }

            return "";
        }

        [Function(nameof(Error))]
        public static async Task Error([OrchestrationTrigger] TaskOrchestrationContext context)
        {
            await context.CallActivityAsync(nameof(SayHello), "throwing");
            throw new InvalidOperationException("This orchestration throws.");
        }

        [Function(nameof(Sub))]
        public static async Task Sub([OrchestrationTrigger] TaskOrchestrationContext context, string activity)
        {
            await context.CallActivityAsync(activity);
        }

        [Function(nameof(All))]
        public static async Task All([OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(All));
            await context.CallSubOrchestratorAsync(nameof(Chain));
            await Task.WhenAll(
                context.CallSubOrchestratorAsync(nameof(Fan)), context.CallSubOrchestratorAsync(nameof(Chain)));

            try
            {
                await context.CallSubOrchestratorAsync(nameof(Error));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "error from orchestration");
                await context.CreateTimer(TimeSpan.FromSeconds(5), default);
            }

            try
            {
                await context.CallSubOrchestratorAsync(nameof(Sub), nameof(Throws));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "error from orchestration");
            }

            Task monitor = context.CallSubOrchestratorAsync(nameof(Monitor));
            await context.CallSubOrchestratorAsync(nameof(Sub), nameof(SayHello));
            await monitor;
        }
        
        // Send an event called "Approval Event"
        [Function(nameof(Interaction))]
        public static async Task<string> Interaction([OrchestrationTrigger] TaskOrchestrationContext context)
        {
            using var timeoutCts = new CancellationTokenSource();
            DateTime dueTime = context.CurrentUtcDateTime.AddMinutes(5);
            Task durableTimeout = context.CreateTimer(dueTime, timeoutCts.Token);

            Task<bool> approvalEvent = context.WaitForExternalEvent<bool>("ApprovalEvent");
            if (approvalEvent == await Task.WhenAny(approvalEvent, durableTimeout))
            {
                timeoutCts.Cancel();
                return "approved";
            }
            else
            {
                return "escalate";
            }
        }

        [Function(nameof(SayHello))]
        public static string SayHello([ActivityTrigger] string name, FunctionContext executionContext)
        {
            ILogger log = executionContext.GetLogger(nameof(SayHello));
            log.LogInformation("Saying hello to {Name}", name);
            return $"Hello {name}!";
        }

        [Function(nameof(Throws))]
        public static string Throws([ActivityTrigger] TaskActivityContext context)
        {
            throw new InvalidOperationException("This activity throws.");
        }

        [Function(nameof(GetStatus))]
        public static string GetStatus([ActivityTrigger] int numTries)
        {
            if (numTries == 3)
            {
                return "Completed";
            }

            return "In Progress";
        }
    }
}
