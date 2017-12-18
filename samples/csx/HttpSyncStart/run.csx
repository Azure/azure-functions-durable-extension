#r "Microsoft.Azure.WebJobs.Extensions.DurableTask"
#r "Newtonsoft.Json"

using System.Net;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;

private const string Timeout = "timeout";
private const string RetryInterval = "retryInterval";

public static async Task<HttpResponseMessage> Run(
    HttpRequestMessage req,
    DurableOrchestrationClient starter,
    string functionName,
    TraceWriter log)
{
    // Function input comes from the request content.
    dynamic eventData = await req.Content.ReadAsAsync<object>();
    string instanceId = await starter.StartNewAsync(functionName, eventData);
    
    log.Info($"Started orchestration with ID = '{instanceId}'.");

    //Get query string parameters and add them to a dictionary
    var queryPairs = req.RequestUri.Query.Replace("?", string.Empty)
        .Split(new[] {"&"}, StringSplitOptions.None);
    var parameters = queryPairs.Select(
        item => item.Split(new[] {"="}, StringSplitOptions.None))
        .Where(queryPairElementsArray => queryPairElementsArray.Length > 1).ToDictionary(
        queryPairElementsArray => queryPairElementsArray[0],
        queryPairElementsArray => queryPairElementsArray[1]);

        TimeSpan? timeout = null;
        TimeSpan? retryInterval = null;

        if (parameters.ContainsKey(Timeout))
        {
            double.TryParse(parameters[Timeout], out var timeoutValue);
            timeout = TimeSpan.FromSeconds(timeoutValue);
        }
        if (parameters.ContainsKey(RetryInterval))
        {
            double.TryParse(parameters[RetryInterval], out var retryIntervalValue);
            retryInterval = TimeSpan.FromSeconds(retryIntervalValue);
        }

        return await starter.WaitForCompletionOrCreateCheckStatusResponseAsync(
            req,
            instanceId,
            timeout,
            retryInterval);
}
