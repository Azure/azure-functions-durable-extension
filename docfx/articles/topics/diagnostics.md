# Diagnostics
There are several options for diagnosing issues with Durable Functions. Some of these options are the same for regular functions and some of them are unique to Durable Functions. This article goes into detail about what options are available.

## Logging
The Durable Functions host extension automatically emits semi-structured logs as it executes orchestrator and activity functions. These logs live alongside the application logs and can be used to monitor the behavior of your orchestrations. To give an example, consider the following orchestrator function:

```cs
#r "Microsoft.Azure.WebJobs.Extensions.DurableTask"

public static async Task<List<string>> Run(DurableOrchestrationContext context)
{
    var outputs = new List<string>();

    outputs.Add(await context.CallFunctionAsync<string>("E1_SayHello", "Tokyo"));
    outputs.Add(await context.CallFunctionAsync<string>("E1_SayHello", "Seattle"));

    // returns ["Hello Tokyo!", "Hello Seattle!"]
    return outputs;
}
```

Below are the log statements that a single execution emits into the host logs. It includes both logging from the host and from Durable Function triggers. Because of the replay behavior of the Durable Task Framework dispatcher, you will see several redundant log entries for replayed actions. One one hand this can be useful to understand the replay behavior of the core engine. On the other hand, this can be noisy and make it hard to understand the logical flow of the orchestration.

<pre>
17:41:04.555 [DF] 4f48: Starting function 'E1_HelloSequence', version ''. IsReplay: False. Input: (4 bytes)
17:41:04.571 Executing 'Functions.E1_HelloSequence' (Reason='', Id=968286d0-d695-425f-af9e-8e5d6992340a)
17:41:04.571 Function started (Id=968286d0-d695-425f-af9e-8e5d6992340a)
17:41:04.711 [DF] 4f48: Scheduling function 'E1_SayHello', version ''. reason: E1_HelloSequence. IsReplay: False.
17:41:04.899 [DF] 4f48: Starting function 'E1_SayHello', version ''. IsReplay: False. Input: (9 bytes)
17:41:04.899 Executing 'Functions.E1_SayHello' (Reason='', Id=b35b27b9-a710-487e-abbc-34ebc60ca6e0)
17:41:04.915 Function started (Id=b35b27b9-a710-487e-abbc-34ebc60ca6e0)
17:41:04.915 Function completed (Success, Id=b35b27b9-a710-487e-abbc-34ebc60ca6e0, Duration=7ms)
17:41:04.915 Executed 'Functions.E1_SayHello' (Succeeded, Id=b35b27b9-a710-487e-abbc-34ebc60ca6e0)
17:41:04.915 [DF] 4f48: Function 'E1_SayHello', version '' completed. ContinuedAsNew: False. IsReplay: False. Output: (14 bytes)
17:41:05.008 [DF] 4f48: Starting function 'E1_HelloSequence', version ''. IsReplay: True. Input: (4 bytes)
17:41:05.008 Executing 'Functions.E1_HelloSequence' (Reason='', Id=dd5f2f4a-48f0-4f45-aad8-2416a9c44b12)
17:41:05.008 Function started (Id=dd5f2f4a-48f0-4f45-aad8-2416a9c44b12)
17:41:05.008 [DF] 4f48: Scheduling function 'E1_SayHello', version ''. reason: E1_HelloSequence. IsReplay: True.
17:41:05.008 [DF] 4f48: Scheduling function 'E1_SayHello', version ''. reason: E1_HelloSequence. IsReplay: False.
17:41:05.024 [DF] 4f48: Starting function 'E1_SayHello', version ''. IsReplay: False. Input: (11 bytes)
17:41:05.024 Executing 'Functions.E1_SayHello' (Reason='', Id=17b1dd65-07e7-46db-8133-6c93a05962ed)
17:41:05.024 Function started (Id=17b1dd65-07e7-46db-8133-6c93a05962ed)
17:41:05.024 Function completed (Success, Id=17b1dd65-07e7-46db-8133-6c93a05962ed, Duration=0ms)
17:41:05.024 Executed 'Functions.E1_SayHello' (Succeeded, Id=17b1dd65-07e7-46db-8133-6c93a05962ed)
17:41:05.024 [DF] 4f48: Function 'E1_SayHello', version '' completed. ContinuedAsNew: False. IsReplay: False. Output: (16 bytes)
17:41:05.055 [DF] 4f48: Starting function 'E1_HelloSequence', version ''. IsReplay: True. Input: (4 bytes)
17:41:05.055 Executing 'Functions.E1_HelloSequence' (Reason='', Id=4c5aa70a-e1e7-4ccc-9b35-4ea778aa1e61)
17:41:05.055 Function started (Id=4c5aa70a-e1e7-4ccc-9b35-4ea778aa1e61)
17:41:05.055 [DF] 4f48: Scheduling function 'E1_SayHello', version ''. reason: E1_HelloSequence. IsReplay: True.
17:41:05.055 [DF] 4f48: Function 'E1_SayHello', version '' completed. ContinuedAsNew: False. IsReplay: True. Output: (replayed)
17:41:05.055 [DF] 4f48: Scheduling function 'E1_SayHello', version ''. reason: E1_HelloSequence. IsReplay: True.
17:41:05.055 Function completed (Success, Id=4c5aa70a-e1e7-4ccc-9b35-4ea778aa1e61, Duration=1ms)
17:41:05.055 Executed 'Functions.E1_HelloSequence' (Succeeded, Id=4c5aa70a-e1e7-4ccc-9b35-4ea778aa1e61)
17:41:05.055 [DF] 4f48: Function 'E1_HelloSequence', version '' completed. ContinuedAsNew: False. IsReplay: False. Output: (33 bytes)
</pre>

There is a lot here, but there are a few important things to notice:

- Per the host logs, **E1_HelloSequence** is started *three* times and completed *once*. This is an artifact of the replay behavior.
- Per the host logs, **E1_SayHello** is executed exactly two times. This is expected because it is called twice in the source code and activity functions are never replayed.
- Each of the Durable Functions trigger logs are prefixed with `[DF]` to distinguish it from host logs.
- There is an `IsReplay` flag in each of the traces with a value of `True` or `False` indicating whether a trace was generated as part of replay execution.

To filter out the replay actions to get the *logical* flow of execution, you can use a simple `grep` or `findstr` expression to filter out all the replay traces (e.g. `findstr /C:"IsReplay: False"`):

<pre>
17:41:04.555 [DF] 4f48: Starting function 'E1_HelloSequence', version ''. IsReplay: False. Input: (4 bytes)
17:41:04.711 [DF] 4f48: Scheduling function 'E1_SayHello', version ''. reason: E1_HelloSequence. IsReplay: False.
17:41:04.899 [DF] 4f48: Starting function 'E1_SayHello', version ''. IsReplay: False. Input: (9 bytes)
17:41:04.915 [DF] 4f48: Function 'E1_SayHello', version '' completed. ContinuedAsNew: False. IsReplay: False. Output: (14 bytes)
17:41:05.008 [DF] 4f48: Scheduling function 'E1_SayHello', version ''. reason: E1_HelloSequence. IsReplay: False.
17:41:05.024 [DF] 4f48: Starting function 'E1_SayHello', version ''. IsReplay: False. Input: (11 bytes)
17:41:05.024 [DF] 4f48: Function 'E1_SayHello', version '' completed. ContinuedAsNew: False. IsReplay: False. Output: (16 bytes)
17:41:05.055 [DF] 4f48: Function 'E1_HelloSequence', version '' completed. ContinuedAsNew: False. IsReplay: False. Output: (33 bytes)
</pre>

This reduced view more closely resembles what you would expect when looking at the source code.

Note that for the best monitoring and diagnostics experience, it is recommended that you enable [Application Insights integration](https://blogs.msdn.microsoft.com/appserviceteam/2017/04/06/azure-functions-application-insights/). This will allow you to store execution logs for longer periods of time and do more efficient query and analysis.

> [!TIP]
> When emitting log statements in the orchestrator function, if you want to only log on non-replay execution, you can write a conditional expression to log only if <xref:Microsoft.Azure.WebJobs.DurableOrchestrationContext.IsReplaying> is equal to `false`.

> [!WARNING]
> The monitoring tab in the Functions portal and the WebJobs dashboard of the Kudu website show all durable function executions, including replays. However, these components were not designed to understand the replay behavior of orchestrator functions. For example, you can expect to see many orchestrator function executions with a warning status because they never complete (as we observed earlier when looking at the host logs). https://github.com/Azure/azure-webjobs-sdk-script-pr/issues/33.

## Debugging
Azure Functions supports debugging function code directly and that same support carries forward to Durable Functions, whether running in Azure or locally. However, there are a few behaviors to be aware of when debugging:

* **Replay**: Orchestrator functions regularly replay when new inputs are received. This means a single *logical* execution of an orchestrator function can result in hitting the same breakpoint multiple times, especially if it is set early in the function code.
* **Await**: Whenever an `await` is encountered, it yields control back to the Durable Task Framework dispatcher. If this is the first time a particular `await` has been encountered, the associated task is *never* resumed. Because the task never resumes, stepping *over* the await (e.g. F10 in Visual Studio) is not actually possible.
* **Messaging Timeouts**: Durable Functions internally uses queue messages to drive execution of both orchestrator functions and activity functions. In a multi-VM environment, breaking into the debugging for extended periods of time could cause a another VM to pick up the message, resulting in duplicate execution. This behavior exists for regular queue-trigger functions as well, but is important to point out in this context since the queues are an implementation detail.

> [!TIP]
> When setting breakpoints, if you want to only break on non-replay execution, you can set a conditional breakpoint which breaks only if <xref:Microsoft.Azure.WebJobs.DurableOrchestrationContext.IsReplaying> is equal to `false`.

## Storage
By default Durable Functions stores state in Azure Storage. This means you can inspect the state of your orchestrations using tools such as [Microsoft Azure Storage Explorer](https://docs.microsoft.com/en-us/azure/vs-azure-tools-storage-manage-with-storage-explorer).

<img src="~/images/storage-explorer.png"/>

This is useful for debugging because you see exactly what state an orchestration may be in. Messages in the queues can also be examined to learn what work is pending (or stuck in some cases).

> [!WARNING]
> While it's easy and convenient to see orchestration state in Azure Storage, you should not take any dependency on the storage entities or schemas as the specifics of their usage may change as the Durable Functions extension evolves.