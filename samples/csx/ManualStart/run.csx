#r "Microsoft.Azure.WebJobs.Extensions.DurableTask"
#r "Microsoft.Extensions.Logging"

using System;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

public static async Task Run(string functionName, IDurableOrchestrationClient starter, ILogger log)
{
    log.LogInformation($"Starting orchestration named: {functionName}");
    string instanceId = await starter.StartNewAsync(functionName, null);
    log.LogInformation($"Started orchestration with ID = '{instanceId}'.");   
}