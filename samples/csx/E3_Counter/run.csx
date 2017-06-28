#r "Microsoft.Azure.WebJobs.Extensions.DurableTask"

public static async Task<int> Run(
    DurableOrchestrationContext counterContext,
    TraceWriter log)
{
    int counterState = counterContext.GetInput<int>();
    log.Info($"Current counter state is {counterState}. Waiting for next operation.");

    string operation = await counterContext.WaitForExternalEvent<string>("operation");
    log.Info($"Received '{operation}' operation.");

    operation = operation?.ToLowerInvariant();
    if (operation == "incr")
    {
        counterState++;
    }
    else if (operation == "decr")
    {
        counterState--;
    }
    
    if (operation != "end")
    {
        counterContext.ContinueAsNew(counterState);
    }

    return counterState;
}
