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
        public static Task OOMOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            // this orchestrator is not deterministic, on purpose.
            // we use the non-determinism to force an OOM exception on only the first replay
            
            // check if a file named "replayEvidence" exists in source code directory, create it if it does not.
            // From experience, this code runs in `<sourceCodePath>/bin/output/`, so we store the file two directories above.
            // We do this because the /bin/output/ directory gets overridden during the build process, which happens automatically
            // when `func host start` is re-invoked.
            string evidenceFile = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "..", "..", "replayEvidence");
            bool isTheFirstReplay = !System.IO.File.Exists(evidenceFile);
            if (isTheFirstReplay)
            {
                System.IO.File.Create(evidenceFile).Close();

                // force the process to run out of memory
                List<byte[]> data = new List<byte[]>();

                for (int i = 0; i < 10000000; i++)
                {
                    data.Add(new byte[1024 * 1024 * 1024]);
                }

                // we expect the code to never reach this statement, it should OOM.
                // we throw just in case the code does not time out. This should fail the test
                throw new Exception("this should never be reached");
            }
            else {
                // if it's not the first replay, delete the evidence file and return
                System.IO.File.Delete(evidenceFile);
                return Task.CompletedTask;
            }
        }
        
        [Function(nameof(ProcessExitOrchestrator))]
        public static Task ProcessExitOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            // this orchestrator is not deterministic, on purpose.
            // we use the non-determinism to force a sudden process exit on only the first replay
            
            // check if a file named "replayEvidence" exists in source code directory, create it if it does not.
            // From experience, this code runs in `<sourceCodePath>/bin/output/`, so we store the file two directories above.
            // We do this because the /bin/output/ directory gets overridden during the build process, which happens automatically
            // when `func host start` is re-invoked.
            string evidenceFile = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "..", "..", "replayEvidence");
            bool isTheFirstReplay = !System.IO.File.Exists(evidenceFile);
            if (isTheFirstReplay)
            {
                System.IO.File.Create(evidenceFile).Close();

                // force sudden crash
                Environment.FailFast("Simulating crash!");
                throw new Exception("this should never be reached");
            }
            else {
                // if it's not the first replay, delete the evidence file and return
                System.IO.File.Delete(evidenceFile);
                return Task.CompletedTask;
            }
        }

        [Function(nameof(TimeoutOrchestrator))]
        public static Task TimeoutOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            // this orchestrator is not deterministic, on purpose.
            // we use the non-determinism to force a timeout on only the first replay
            
            // check if a file named "replayEvidence" exists in source code directory, create it if it does not.
            // From experience, this code runs in `<sourceCodePath>/bin/output/`, so we store the file two directories above.
            // We do this because the /bin/output/ directory gets overridden during the build process, which happens automatically
            // when `func host start` is re-invoked.
            string evidenceFile = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "..", "..", "replayEvidence");
            bool isTheFirstReplay = !System.IO.File.Exists(evidenceFile);

            if (isTheFirstReplay)
            {
                System.IO.File.Create(evidenceFile).Close();
                
                // force the process to timeout after a 1 minute wait
                System.Threading.Thread.Sleep(TimeSpan.FromMinutes(1));
                
                // we expect the code to never reach this statement, it should time out.
                // we throw just in case the code does not time out. This should fail the test
                throw new Exception("this should never be reached");
            }
            else {
                // if it's not the first replay, delete the evidence file and return
                System.IO.File.Delete(evidenceFile);
                return Task.CompletedTask;
            }
        }

        [Function("durable_HttpStartOOMOrchestrator")]
        public static async Task<HttpResponseData> HttpStartOOMOrchestrator(
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
        public static async Task<HttpResponseData> HttpStartProcessExitOrchestrator(
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
        public static async Task<HttpResponseData> HttpStartTimeoutOrchestrator(
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
