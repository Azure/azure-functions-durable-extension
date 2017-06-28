# Error Handling & Compensation
Durable Function orchestrations are implemented in pure code and therefore leverage the error handling capabilities of the orchestrator function's programming language. With this in mind, there really aren't any new concepts you need to learn about when incorporating error handling and compensation into your orchestrations. However, there are still a few important behaviors to be aware of when planning for error handling.

## Errors in activity functions
Any exception that is thrown in an activity function is marshalled back to the orchestrator function and thrown as a `TaskFailedException`. Users can write the appropriate error handling and compensation code that suits their needs around this.

For example, consider the following orchestrator function which transfers funds from one account to another:

```csharp
#r "Microsoft.Azure.WebJobs.Extensions.DurableTask"

public static async Task Run(DurableOrchestrationContext context)
{
    var transferDetails = ctx.GetInput<TransferOperation>();

    await context.CallFunctionAsync("DebitAccount",
        new
        { 
            Account = transferDetails.SourceAccount,
            Amount = transferDetails.Amount
        });

    bool credited;
    try
    {
        await context.CallFunctionAsync("CreditAccount",         
            new
            { 
                Account = transferDetails.DestinationAccount,
                Amount = transferDetails.Amount
            });

        credited = true;
    }
    catch (TaskFailedException)
    {
        credited = false;
    }

    if (!credited)
    {
        // Refund the source account.
        // Another try/catch could be used here based on the needs of the application.
        await context.CallFunctionAsync("CreditAccount",         
            new
            { 
                Account = transferDetails.SourceAccount,
                Amount = transferDetails.Amount
            });
    }
}
```
> [!NOTE]
> The above code requires a DLL reference to DurableTask.Framework assembly, which is not ideal.
> https://github.com/Azure/azure-webjobs-sdk-script-pr/issues/41

If the call to the **CreditAccount** function fails for the destination account, the orchestrator function compensates for this by crediting the funds back to the source account.

## Retry on failure
There is currently no first-class support for retrying function calls which fail with an error. However, it is still possible to implement retry manually using code, like in the following example:

```csharp
public static async Task<bool> Run(DurableOrchestrationContext context)
{
    const int MaxRetries = 3;

    for (int i = 0; i <= MaxRetries; i++)
    {
        try
        {
            await context.CallFunctionAsync("FlakyFunction");
            return true;
        }
        catch { }
    }

    return false;
}
```

> [!NOTE]
> Automatic retry is a planned feature for public preview: https://github.com/Azure/azure-webjobs-sdk-script-pr/issues/42

## Function timeouts
It's possible that you may want to abandon a function call within an orchestrator function if it is taking too long to complete. The proper way to do this today is by creating a durable timer using `context.CreateTimer` in conjunction with `Task.WhenAny`, as in the following example:

```csharp
public static async Task<bool> Run(DurableOrchestrationContext context)
{
    TimeSpan timeout = TimeSpan.FromSeconds(30);
    DateTime deadline = context.CurrentUtcDateTime.Add(timeout);

    using (var cts = new CancellationTokenSource())
    {
        Task activityTask = context.CallFunctionAsync("FlakyFunction");
        Task timeoutTask = context.CreateTimer(deadline, cts.Token);

        Task winner = await Task.WhenAny(activityTask, timeoutTask);
        if (winner == activityTask)
        {
            // success case
            cts.Cancel();
            return true;
        }
        else
        {
            // timeout case
            return false;
        }
    }
}
```

For more information on timers, see the [Durable Timers](./timers.md) topic.

## Unhandled exceptions
If an orchestrator function fails with an unhandled exception, the details of the exception will be logged and the instance will complete with a `Failed` status.

> [!NOTE]
> Failed instances cannot be recovered currently. However, it would be extremely useful if they could be recovered in certain situations to help mitigate potential data loss.
> https://github.com/Azure/azure-webjobs-sdk-script-pr/issues/20