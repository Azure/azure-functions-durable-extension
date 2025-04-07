using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json.Nodes;

namespace Microsoft.Azure.Durable.Tests.E2E;

public static class LargeOutputOrchestrator
{
    [Function(nameof(LargeOutputOrchestrator))]
    public static async Task<List<string>> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(HelloCities));
        int size = context.GetInput<int>();

        logger.LogInformation("Saying hello.");
        var outputs = new List<string>();

        outputs.Add(await context.CallActivityAsync<string>(nameof(LargeOutputSayHello), "Tokyo"));

        // Add a large message to the outputs that exceeds the Azure Storage Queue message size limit (64 KB),
        // so that blobs will be used instead. 
        outputs.Add(GenerateLargeString(size)); 

        return outputs;
    }

    [Function(nameof(LargeOutputSayHello))]
    public static string LargeOutputSayHello([ActivityTrigger] string name, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("SayHello");
        logger.LogInformation("Saying hello to {name}.", name);
        return $"Hello {name}!";
    }

    [Function("LargeOutputOrchestrator_HttpStart")]
    public static async Task<HttpResponseData> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("LargeOutputOrchestrator_HttpStart");
        int size = await req.ReadFromJsonAsync<int>();
        
        // Function input comes from the request content.
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(LargeOutputOrchestrator), size);

        logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

        // Returns an HTTP 202 response with an instance management payload.
        // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }

    [Function("LargeOutputOrchestrator_Query_Output")]
    public static async Task<HttpResponseData> QueryOutput(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string id)
    {
        OrchestrationMetadata? metadata = await client.GetInstancesAsync(instanceId: id, getInputsAndOutputs:true);
   
        HttpResponseData response;
        if (metadata == null)
        {
            response = req.CreateResponse(HttpStatusCode.NotFound);
            await response.WriteStringAsync("Orchestration metadata not found.");
            return response; // Return a 404 response if metadata is null
        }

        var output = metadata.ReadOutputAs<JsonArray>();

        response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(output.ToString());

        return response;
    }

    static string GenerateLargeString(int size)
    {
        return new string('A', size * 1024);
    }
}