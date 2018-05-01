#r "Microsoft.Azure.WebJobs.Extensions.DurableTask"
#r "Microsoft.Extensions.Logging"
#r "Newtonsoft.Json"

using System.Net;
using System.Net.Http.Headers;

private const string Timeout = "timeout";
private const string RetryInterval = "retryInterval";

public static async Task<HttpResponseMessage> Run(
    HttpRequestMessage req,
    DurableOrchestrationClient starter,
    string functionName,
    ILogger log)
{
    // Function input comes from the request content.
    dynamic eventData = await req.Content.ReadAsAsync<object>();
    string instanceId = await starter.StartNewAsync(functionName, eventData);
    
    log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

    TimeSpan timeout = TimeSpan.FromSeconds(6);
    TimeSpan retryInterval = TimeSpan.FromSeconds(0.5);

    return await starter.WaitForCompletionOrCreateCheckStatusResponseAsync(
        req,
        instanceId,
        timeout,
        retryInterval);
}