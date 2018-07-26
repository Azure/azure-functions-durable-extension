#r "Microsoft.Azure.WebJobs.Extensions.DurableTask"

// Function chaining refers to the pattern of executing a sequence of functions in a particular order.
// This orchestrator performs three activity functions sequentially.
// More on running this sample here: https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-sequence

public static async Task<List<string>> Run(DurableOrchestrationContext context)
{
    var outputs = new List<string>();

    outputs.Add(await context.CallActivityAsync<string>("E1_SayHello", "Tokyo"));
    outputs.Add(await context.CallActivityAsync<string>("E1_SayHello", "Seattle"));
    outputs.Add(await context.CallActivityAsync<string>("E1_SayHello", "London"));

    // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
    return outputs;
}