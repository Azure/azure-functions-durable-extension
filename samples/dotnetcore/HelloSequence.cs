using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.ContextImplementations;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;

namespace DurableClientSampleFunctionApp
{
    public class HelloSequence
    {
        private readonly IDurableClientFactory _clientFactory;

        public HelloSequence(IDurableClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        [FunctionName("CallHelloSequence")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            var client = _clientFactory.CreateClient(new DurableClientOptions
            {
                ConnectionName = "Storage",
                TaskHub = "<TaskHubName>"
            });

            string instanceId = await client.StartNewAsync("E1_HelloSequence");

            DurableOrchestrationStatus status = await client.GetStatusAsync(instanceId);

            while (status.RuntimeStatus == OrchestrationRuntimeStatus.Pending ||
                    status.RuntimeStatus == OrchestrationRuntimeStatus.Running ||
                    status.RuntimeStatus == OrchestrationRuntimeStatus.ContinuedAsNew)
            {
                await Task.Delay(10000);
                status = await client.GetStatusAsync(instanceId);
            }

            return new ObjectResult(status);
        }
    }
}
