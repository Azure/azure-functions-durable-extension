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

    await context.CallActivityAsync("DebitAccount",
        new
        { 
            Account = transferDetails.SourceAccount,
            Amount = transferDetails.Amount
        });

    try
    {
        await context.CallActivityAsync("CreditAccount",         
            new
            { 
                Account = transferDetails.DestinationAccount,
                Amount = transferDetails.Amount
            });
    }
    catch (Exception)
    {
        // Refund the source account.
        // Another try/catch could be used here based on the needs of the application.
        await context.CallActivityAsync("CreditAccount",         
            new
            { 
                Account = transferDetails.SourceAccount,
                Amount = transferDetails.Amount
            });
    }
}
```

If the call to the **CreditAccount** function fails for the destination account, the orchestrator function compensates for this by crediting the funds back to the source account.

## Automatic retry on failure
When calling activity functions or sub-orchestration functions it is possible to specify an automatic retry policy. The below example attempts to call a function up to 3 times and waits 5 seconds in between each retry:

```csharp
public static async Task Run(DurableOrchestrationContext context)
{
    var retryOptions = new RetryOptions(
        firstRetryInterval: TimeSpan.FromSeconds(5),
        maxNumberOfAttempts: 3);

    await ctx.CallActivityWithRetryAsync("FlakyFunction", retryOptions);
    
    // ...
}
```
As shown in the code, the <xref:Microsoft.Azure.WebJobs.DurableOrchestrationContext.CallActivityWithRetryAsync> API takes a <xref:Microsoft.Azure.WebJobs.RetryOptions> parameter which describes the retry policy. Sub-orchestration calls using the <xref:Microsoft.Azure.WebJobs.DurableOrchestrationContext.CallSubOrchestratorWithRetryAsync> API can also leverage these same retry policies.

There are several options for customizing the automatic retry policy. They include the following:

* **Max number of attempts**: The maximum number of retry attempts.
* **First retry interval**: The amount of time to wait before the first retry attempt.
* **Backoff coefficient**: The coefficient used to determine rate of increase of backoff. Defaults to 1.
* **Max retry interval**: The maximum amount of time to wait in between retry attempts.
* **Retry timeout**: The maximum amount of time to spend doing retries. The default behavior is to retry indefinitely.
* **Custom**: A user-defined callback can be specified which determines whether or not a function call should be retried.

## Function timeouts
It's possible that you may want to abandon a function call within an orchestrator function if it is taking too long to complete. The proper way to do this today is by creating a durable timer using `context.CreateTimer` in conjunction with `Task.WhenAny`, as in the following example:

```csharp
public static async Task<bool> Run(DurableOrchestrationContext context)
{
    TimeSpan timeout = TimeSpan.FromSeconds(30);
    DateTime deadline = context.CurrentUtcDateTime.Add(timeout);

    using (var cts = new CancellationTokenSource())
    {
        Task activityTask = context.CallActivityAsync("FlakyFunction");
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