# Singleton Orchestrators
It's often the case when building background jobs or actor-style orchestrations that you need to ensure you have exactly one instance of a particular orchestrator running at any given time. This can be done by assigning a specific instance ID to an orchestrator when creating it.

## Singleton Trigger Example
The following C# example shows an HTTP-trigger function that creates a singleton background job orchestration. It uses a well-known instance ID to ensure that only one instance exists.

```cs
[FunctionName("EnsureSingletonTrigger")]
public static async Task<HttpResponseMessage> Ensure(
    [HttpTrigger(AuthorizationLevel.Function, methods: "post")] HttpRequestMessage req,
    [OrchestrationClient] DurableOrchestrationClient starter,
    TraceWriter log)
{
    // Ensure only one instance is ever running at a time
    const string OrchestratorName = "MySingletonOrchestrator";
    const string InstanceId = "MySingletonInstanceId";

    var existingInstance = await starter.GetStatusAsync(InstanceId);
    if (existingInstance == null)
    {
        log.Info($"Creating singleton instance with ID = {InstanceId}...");
        await starter.StartNewAsync(OrchestratorName, InstanceId, input: null);
    }

    return starter.CreateCheckStatusResponse(req, InstanceId);
}
```

By default, instance IDs are randomly generated GUIDs. But notice in this case the trigger function uses a predefined `InstanceId` variable with a value of `MySingletonInstanceId` to pre-assign an instance ID to the orchestrator function. This allows the trigger to check and see whether the well-known instance is already running using <xref:Microsoft.Azure.WebJobs.DurableOrchestrationClient.GetStatusAsync*> and checking for a null response.

The implementation details of the orchestrator function do not actually matter. It could be a regular orchestrator function which starts and completes, or it could be one which runs forever (i.e. an [Eternal Orchestration](~/articles/topics/eternal-orchestrations.md)). The important point is that there is only ever one instance running at a time.

> [!NOTE]
> If the singleton orchestration instance terminates, fails, or completes, it will not be possible to recreate it using the same ID. In those cases, you should be prepared to recreate it using a new instance ID.
