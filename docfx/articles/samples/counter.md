# Stateful Actor - Counter
The [Actor model](https://en.wikipedia.org/wiki/Actor_model) is an advanced stateful programming pattern that is becoming more common in distributed computing, particularly in the cloud. It is assumed that the reader is already familiar with the actor model and how actors are used, so we won't be going into those details here. The code in this sample demonstrates how Durable Functions can be used to implement the actor model. The specific example is a simple *counter* singleton object which supports *increment* and *decrement* operations.

## Before you begin
If you haven't done so already, make sure to read the [overview](~/articles/overview.md) before jumping into samples. It will really help ensure everything you read below makes sense.

All samples are combined into a single function app package. To get started with the samples, follow the setup steps below that are relevant for your development environment:

### For Visual Studio Development (Windows Only)
1. Follow the [installation instructions](~/articles/installation.md) to configure Durable Functions for Visual Studio development.
2. Download the [VSDFSampleApp.zip](~/files/VSDFSampleApp.zip) package.
3. Uprotect the zip file: right-click `VSDFSampleApp.zip` --> **Properties** --> **Unprotect**. 
4. Unzip the sample package and open the solution file in Visual Studio 2017 (version 15.3).
5. Install and run the [Azure Storage Emulator](https://docs.microsoft.com/en-us/azure/storage/storage-use-emulator). Alternatively, you can update the `local.appsettings.json` file with real Azure Storage connection strings.
6. The sample can now be run locally via F5. If you want to publish the solution to Azure, follow the [installation instructions](~/articles/installation.md) to configure Durable Functions in Azure.

### For Azure Portal Development
1. Create a new function app at https://functions.azure.com/signin.
2. Follow the [installation instructions](~/articles/installation.md) to configure Durable Functions.
3. Download the [DFSampleApp.zip](~/files/DFSampleApp.zip) package.
4. Unzip the sample package file into `D:\home\site\wwwroot` using Kudu or FTP.

The code snippets and binding references below are specific to the portal experience, but have a direct mapping to the equivalent Visual Studio development experience.

This article will specifically walk through the following function in the sample app:

* **E3_Counter**

> [!NOTE]
> This walkthrough assumes you have already gone through the [Hello Sequence](./sequence.md) sample walkthrough. If you haven't done so already, it is recommended to first go through that walkthrough before starting this one.

## Scenario overview
The counter scenario is very simple to understand, but surprisingly difficult to implement using regular stateless functions. The main challenge you have is managing **concurrency**. Operations like *increment* and *decrement* need to be atomic, or else there could be race conditions that cause operations to overwrite each other.

Using a single VM to host the counter data is one option, but this is expensive and managing **reliability** can be a challenge since a single VM will need to be periodically rebooted. You could alternatively use a distributed platform with synchronization tools like blob leases to help manage concurrency, but this introduces a great deal of **complexity**.

Durable Functions makes this kind of scenario trivial to implement because orchestration instances affinitized to a single VM and orchestrator function execution is always single-threaded. Not only that, but they are long-running, stateful, and can react to external events, making them look and behave just like a reliable actor. The sample code below will demonstrate how to implement such a counter as a long-running orchestrator function.

## The counter orchestration
The **E3_Counter** function uses the standard function.json for orchestrator functions.

[!code-json[Main](~/../samples/csx/E3_Counter/function.json)]

Here is the code which implements the function:

[!code-csharp[Main](~/../samples/csx/E3_Counter/run.csx)]

This orchestrator function essentially does the following:

1. Listens for an external event named *operation* using <xref:Microsoft.Azure.WebJobs.DurableOrchestrationContext.WaitForExternalEvent*>>.
2. Increments or decrements the `counterState` local variable depending on the operation requested.
3. Restarts the orchestrator using the <xref:Microsoft.Azure.WebJobs.DurableOrchestrationContext.ContinueAsNew*> method, setting the latest value of `counterState` as the new input.
4. Continues running forever or until an *end* message is received.

This is an example of an *eternal orchestration* - i.e. one that potentially never ends. It responds to messages sent to it using the <xref:Microsoft.Azure.WebJobs.DurableOrchestrationClient.RaiseEventAsync*> method, which can be called by any non-orchestrator function.

One unique characteristic of this orchestrator function is that it effectively has no history: the <xref:Microsoft.Azure.WebJobs.DurableOrchestrationContext.ContinueAsNew*> method will reset the history after each processed event. This is the prefered way to implement an orchestrator which has an arbitrary lifetime as using a `while` loop could cause the orchestrator function's history to grow unbounded, resulting in unnecessarily high memory usage.

> [!NOTE]
> The <xref:Microsoft.Azure.WebJobs.DurableOrchestrationContext.ContinueAsNew*> method has other interesting use-cases besides just eternal orchestrations. See the [Eternal Orchestrations](../topics/eternal-orchestrations.md) topic guide for more information.

## Running the sample
Using the HTTP-triggered functions included in the sample, you can start the orchestration using the below HTTP POST request. We don't include any content in this request, which allows `counterState` to start at zero (the default value for `int`).

```plaintext
POST http://{host}/orchestrators/E3_Counter HTTP/1.1
Content-Length: 0
```

```plaintext
HTTP/1.1 202 Accepted
Content-Length: 719
Content-Type: application/json; charset=utf-8
Location: http://{host}/admin/extensions/DurableTaskConfiguration/instances/bcf6fb5067b046fbb021b52ba7deae5a?taskHub=DurableFunctionsHub&connection=Storage

{"id":"bcf6fb5067b046fbb021b52ba7deae5a","statusQueryGetUri":"http://{host}/admin/extensions/DurableTaskConfiguration/instances/bcf6fb5067b046fbb021b52ba7deae5a?taskHub=DurableFunctionsHub&connection=Storage","sendEventPostUri":"http://{host}/admin/extensions/DurableTaskConfiguration/instances/bcf6fb5067b046fbb021b52ba7deae5a/raiseEvent/{eventName}?taskHub=DurableFunctionsHub&connection=Storage","terminatePostUri":"http://{host}/admin/extensions/DurableTaskConfiguration/instances/bcf6fb5067b046fbb021b52ba7deae5a/terminate?reason={text}&taskHub=DurableFunctionsHub&connection=Storage"}
```

The **E3_Counter** instance starts and then immediately waits for an event to be sent to it using <xref:Microsoft.Azure.WebJobs.DurableOrchestrationClient.RaiseEventAsync*> or using the **sendEventUrl** HTTP POST webhook referenced in the 202 response above. Valid `eventName` values include *incr*, *decr*, and *end*.

> [!NOTE]
> Feel free to take a look at the source code for HttpSendEvent to get an idea of how <xref:Microsoft.Azure.WebJobs.DurableOrchestrationClient.RaiseEventAsync*> is used by client functions.

```plaintext
POST http://{host}/admin/extensions/DurableTaskConfiguration/instances/bcf6fb5067b046fbb021b52ba7deae5a/raiseEvent/operation?taskHub=DurableFunctionsHub&connection=Storage HTTP/1.1
Content-Type: application/json
Content-Length: 6

"incr"
```

You can see the results of the "incr" operation by looking at the function logs in the Azure Functions portal.

```plaintext
2017-06-29T18:54:53.998 Function started (Id=34e34a61-38b3-4eac-b6e2-98b85e32eec8)
2017-06-29T18:54:53.998 Current counter state is 0. Waiting for next operation.
2017-06-29T18:58:01.458 Function started (Id=b45d6c2f-39f3-42a2-b904-7761b2614232)
2017-06-29T18:58:01.458 Current counter state is 0. Waiting for next operation.
2017-06-29T18:58:01.458 Received 'incr' operation.
2017-06-29T18:58:01.458 Function completed (Success, Id=b45d6c2f-39f3-42a2-b904-7761b2614232, Duration=8ms)
2017-06-29T18:58:11.518 Function started (Id=e1f38cb2-546a-404d-ab22-1ac8f81a93d9)
2017-06-29T18:58:11.518 Current counter state is 1. Waiting for next operation.
```

Similarly, if you check the orchestrator status, you should see the `input` field has been set to the updated value (1).

```plaintext
GET http://{host}/admin/extensions/DurableTaskConfiguration/instances/bcf6fb5067b046fbb021b52ba7deae5a?taskHub=DurableFunctionsHub&connection=Storage HTTP/1.1
```

```plaintext
HTTP/1.1 202 Accepted
Content-Length: 129
Content-Type: application/json; charset=utf-8
Location: http://{host}/admin/extensions/DurableTaskConfiguration/instances/bcf6fb5067b046fbb021b52ba7deae5a?taskHub=DurableFunctionsHub&connection=Storage

{"runtimeStatus":"Running","input":1,"output":null,"createdTime":"2017-06-29T18:58:01Z","lastUpdatedTime":"2017-06-29T18:58:11Z"}
```

You can continue sending new operations to this instance and observe its state gets updated accordingly. If you wish to kill the instance, you can do so by sending an *end* operation.

> [!WARNING]
> There are currently race conditions in both the handling of <xref:Microsoft.Azure.WebJobs.DurableOrchestrationClient.RaiseEventAsync*> and the use of <xref:Microsoft.Azure.WebJobs.DurableOrchestrationContext.ContinueAsNew*>. Until these are addressed, it is highly recommended to not send more than one external event to an instance every few seconds.

## Wrapping up
At this point, you should have a better understanding of some of the advanced capabilities of Durable Functions, notably <xref:Microsoft.Azure.WebJobs.DurableOrchestrationContext.WaitForExternalEvent*> and <xref:Microsoft.Azure.WebJobs.DurableOrchestrationContext.ContinueAsNew*>. These tools should enable you to write "eternal orchestrations" and/or implement the stateful actor pattern.