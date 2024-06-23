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
using Microsoft.Extensions.Configuration;

namespace DurableClientSampleFunctionApp
{
    public class ClientFunction
    {
        private readonly IDurableClient _client;

        public ClientFunction(IDurableClientFactory clientFactory, IConfiguration configuration)
        {
            _client = clientFactory.CreateClient(new DurableClientOptions
            {
                ConnectionName = "Storage",
                TaskHub = configuration["TaskHub"]
            });
        }

        [FunctionName("CallHelloSequence")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string instanceId = await _client.StartNewAsync("E1_HelloSequence");

            DurableOrchestrationStatus status = await _client.GetStatusAsync(instanceId);

            while (status.RuntimeStatus == OrchestrationRuntimeStatus.Pending ||
                    status.RuntimeStatus == OrchestrationRuntimeStatus.Running ||
                    status.RuntimeStatus == OrchestrationRuntimeStatus.ContinuedAsNew)
            {
                await Task.Delay(10000);
                status = await _client.GetStatusAsync(instanceId);
            }

            return new ObjectResult(status);
        }
    }
}
