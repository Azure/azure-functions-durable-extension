using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System;

namespace FaultOrchestrators
{
    public static class FaultyOrchestrators
    {
        [Function(nameof(OOMOrchestrator))]
        public static async Task<string> OOMOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            // this orchestrator is not deterministic, on purpose.
            // we use the non-determinism to force an OOM exception on only the first replay
            
            // check if a file named "replayEvidence" exists in the current directory.
            // create it if it does not
            string evidenceFile = "replayEvidence";
            bool isTheFirstReplay = !System.IO.File.Exists(evidenceFile);
            if (isTheFirstReplay)
            {
                System.IO.File.Create(evidenceFile).Close();
            }
            
            // on the very first replay, OOM the process
            if (isTheFirstReplay)
            {
                // force the process to run out of memory
                List<byte[]> data = new List<byte[]>();

                for (int i = 0; i < 10000000; i++)
                {
                    data.Add(new byte[1024 * 1024 * 1024]);
                }
            }
            
            // assuming the orchestrator survived the OOM, delete the evidence file and return
            System.IO.File.Delete(evidenceFile)
            return "done!";
        }
        
        [Function(nameof(ProcessExitOrchestrator))]
        public static async Task<string> ProcessExitOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            // this orchestrator is not deterministic, on purpose.
            // we use the non-determinism to force a sudden process exit on only the first replay
            
            // check if a file named "replayEvidence" exists in the current directory.
            // create it if it does not
            string evidenceFile = "replayEvidence";
            bool isTheFirstReplay = !System.IO.File.Exists(evidenceFile);
            if (isTheFirstReplay)
            {
                System.IO.File.Create(evidenceFile).Close();
            }
            
            // on the very first replay, OOM the process
            if (isTheFirstReplay)
            {
                // force the process to suddenly exit
                Environment.FailFast(-1);
            }
            
            // assuming the orchestrator survived the OOM, delete the evidence file and return
            System.IO.File.Delete(evidenceFile)

            return "done!";
        }

        [Function(nameof(TimeoutOrchestrator))]
        public static async Task<string> TimeoutOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            // this orchestrator is not deterministic, on purpose.
            // we use the non-determinism to force a timeout on only the first replay
            
            // check if a file named "replayEvidence" exists in the current directory.
            // create it if it does not
            string evidenceFile = "replayEvidence";
            bool isTheFirstReplay = !System.IO.File.Exists(evidenceFile);
            if (isTheFirstReplay)
            {
                System.IO.File.Create(evidenceFile).Close();
            }
            
            // on the very first replay, time out the execution
            if (isTheFirstReplay)
            {
                // force the process to timeout after a 1 minute wait
                System.Threading.Thread.Sleep(TimeSpan.FromMinutes(1));
            }
            
            // assuming the orchestrator survived the timeout, delete the evidence file and return
            System.IO.File.Delete(evidenceFile)

            return "done!";
        }

        [Function("durable_HttpStartOOMOrchestrator")]
        public static async Task<HttpResponseData> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("durable_HttpStartOOMOrchestrator");

            // Function input comes from the request content.
            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(OOMOrchestrator));

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            // Returns an HTTP 202 response with an instance management payload.
            // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
            return await client.CreateCheckStatusResponseAsync(req, instanceId);
        }

        [Function("durable_HttpStartProcessExitOrchestrator")]
        public static async Task<HttpResponseData> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("durable_HttpStartProcessExitOrchestrator");

            // Function input comes from the request content.
            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(ProcessExitOrchestrator));

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            // Returns an HTTP 202 response with an instance management payload.
            // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
            return await client.CreateCheckStatusResponseAsync(req, instanceId);
        }

        [Function("durable_HttpStartTimeoutOrchestrator")]
        public static async Task<HttpResponseData> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("durable_HttpStartTimeoutOrchestrator");

            // Function input comes from the request content.
            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(TimeoutOrchestrator));

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            // Returns an HTTP 202 response with an instance management payload.
            // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
            return await client.CreateCheckStatusResponseAsync(req, instanceId);
        }
    }
}
