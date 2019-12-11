#r "Microsoft.Azure.WebJobs.Extensions.DurableTask"
#r "Microsoft.Extensions.Logging"
#r "Newtonsoft.Json"

using System.Net;
using System.Net.Http.Headers;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

public static async Task<HttpResponseMessage> Run(
    HttpRequestMessage req,
    IDurableOrchestrationClient starter,
    string functionName,
    ILogger log)
{
    // Function input comes from the request content.
    object eventData = await req.Content.ReadAsAsync<object>();
    string instanceId = await starter.StartNewAsync(functionName,  eventData);
    
    log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
    
    return starter.CreateCheckStatusResponse(req, instanceId);
}
