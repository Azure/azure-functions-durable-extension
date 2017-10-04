# Sub-Orchestrations
In addition to calling activity functions, orchestrator functions can also call other orchestrator functions. This is useful when you have a library of orchestrator functions and want to build a larger orchestration around them. This is also useful if you need to run multiple instances of an orchestrator function in parallel.

An orchestrator function can call another orchestrator function using the <xref:Microsoft.Azure.WebJobs.DurableOrchestrationContext.CallSubOrchestratorAsync*> API. There is also a <xref:Microsoft.Azure.WebJobs.DurableOrchestrationContext.CallSubOrchestratorWithRetryAsync*> variant which allows specifying automatic retry policies. 

> [!NOTE]
> For more information on automatic retry, see the [Error Handling & Compensation](~/articles/topics/error-handling.md#automatic-retry-on-failure) topic.

These sub-orchestrator functions behave just like activity functions from the caller's perspective. They can return a value, throw an exception, and can be awaited on by the parent orchestrator function.

## Example
The following example illustrates an IoT ("Internet of Things") scenario where there are multiple devices which need to be provisioned. There is a particular orchestration that needs to happen for each of the devices, which might look something like the following:

```csharp
public static async Task DeviceProvisioningOrchestration(
    [OrchestrationTrigger] DurableOrchestrationContext ctx)
{
    string deviceId = ctx.GetInput<string>();

    // Step 1: Create an installation package in blob storage and return a SAS URL.
    Uri sasUrl = await ctx.CallActivityAsync<Uri>("CreateInstallationPackage", deviceId);

    // Step 2: Notify the device that the installation package is ready.
    await ctx.CallActivityAsync("SendPackageUrlToDevice", Tuple.Create(deviceId, sasUrl));

    // Step 3: Wait for the device to acknowledge that it has downloaded the new package.
    await ctx.WaitForExternalEvent<bool>("DownloadCompletedAck");

    // Step 4: ...
}
```

This orchestrator function can be used as-is for one-off device provisioning or it can be part of a larger orchestration. In the latter case, the parent orchestrator function can schedule instances of `DeviceProvisioningOrchestration` using the <xref:Microsoft.Azure.WebJobs.DurableOrchestrationContext.CallSubOrchestratorAsync*> API.

Below is a simple example of this that shows running multiple `DeviceProvisioningOrchestration` orchestrator functions in parallel.

```csharp
[FunctionName("ProvisionNewDevices")]
public static async Task ProvisionNewDevices(
    [OrchestrationTrigger] DurableOrchestrationContext ctx)
{
    string[] deviceIds = await ctx.CallActivityAsync<string[]>("GetNewDeviceIds");

    // Run multiple device provisioning flows in parallel
    var provisioningTasks = new List<Task>();
    foreach (string deviceId in deviceIds)
    {
        Task provisionTask = ctx.CallSubOrchestratorAsync("DeviceProvisioningOrchestration");
        provisioningTasks.Add(provisionTask);
    }

    await Task.WhenAll(provisioningTasks);

    // ...
}
```

