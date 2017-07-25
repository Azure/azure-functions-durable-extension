#r "Microsoft.Azure.WebJobs.Extensions.DurableTask"

using System;

public static async Task Run(string functionName, DurableOrchestrationClient starter, TraceWriter log)
{
    log.Info($"Starting orchestration named: {functionName}");
    string instanceId = await starter.StartNewAsync(functionName, null);
    log.Info($"Started orchestration with ID = '{instanceId}'.");   
}