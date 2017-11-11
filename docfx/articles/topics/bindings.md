# Bindings
## Overview
The Durable Functions extension introduces two new trigger bindings which control the execution of orchestrator and activity functions. It also introduces an output binding which acts as a client into the Durable Functions runtime.

## Orchestration triggers
The orchestration trigger enables you to author durable orchestrator functions. This trigger supports starting new orchestrator function instances and resuming existing orchestrator function instances that are "awaiting" on a task.

When using the Visual Studio tools for Azure Functions, the orchestration trigger is configured using the <xref:Microsoft.Azure.WebJobs.OrchestrationTriggerAttribute> .NET attribute.

When authoring orchestrator functions in scripting languages (e.g. in the Azure management portal), the orchestration trigger is defined by the following JSON object in the `bindings` array of function.json:

```json
{
    "name": "<Name of input parameter in function signature>",
    "orchestration": "<Optional - name of the orchestration>",
    "version": "<Optional - version label of this orchestrator function>",
    "type": "orchestrationTrigger",
    "direction": "in"
}
```

Note the following:
* `orchestration` is the name of the orchestration. This is the value that clients must use when they want to start new instances of this orchestrator function. This property is optional. If not specified, the name of the function is used.
* `version` is a version label of the orchestration. Clients that start a new instance of an orchestration must include the matching version label. This property is optional. If not specified, the empty string is used. For more information on versioning, see the [Versioning](./versioning.md) topic.

Internally this trigger binding polls a series of queues in the default storage account for the function app. These queues are internal implementation details of the extension, which is why they are not explicitly configured in the binding properties.

> [!NOTE]
> Setting values for `orchestration` or `version` properties is not recommended at this time.

### Trigger behavior
Note the following behaviors of the orchestration trigger:
* **single-threading** - a single dispatcher thread is used for all orchestrator function execution on a single host instance. For this reason, it is important to ensure that orchestrator function code is efficient and doesn't perform any I/O. It is also important that this thread does not do any async work except when awaiting on Durable Functions-specific task types.
* **Poising-message handling** - there is no poison message support in orchestration triggers.
* **Message visibility** - orchestration trigger messages are dequeued and kept invisible for a configurable duration. The visibility of these messages is renewed automatically as long as the function app is running and healthy.
* **Return values** - return values are serialized to JSON and persisted to the orchestration history table in Azure Table storage. These return values can be queried by the orchestration client binding, described later.

> [!WARNING]
> Orchestrator functions should never use any input or output bindings other than the orchestration trigger binding. Doing so has the potential to cause problems with the Durable Task extension because those bindings may not obey the single-threading and I/O rules.

### Trigger usage
The orchestration trigger binding supports both inputs and outputs. Here are some things to know about input and output handling:

* **inputs** - orchestration functions only support using <xref:Microsoft.Azure.WebJobs.DurableOrchestrationContext> as a parameter type. Deserialization inputs directly in the function signature is not supported. Code must use the <xref:Microsoft.Azure.WebJobs.DurableOrchestrationContext.GetInput*> method to fetch orchestrator function inputs. These inputs must be JSON-serializable types.
* **outputs** - Orchestration triggers support output values as well as inputs. The return value of the function is used to assign the output value and must be JSON-serializable. If a function returns `Task` or `void` then a `null` value will be saved as the output.

> [!NOTE]
> Orchestration triggers are only supported in C# at this time.

### Trigger sample
The following is an example of what the simplest "Hello World" C# orchestrator function might look like:

```csharp
[FunctionName("HelloWorld")]
public static string Run([OrchestrationTrigger] DurableOrchestrationContext context)
{
    string name = context.GetInput<string>();
    return $"Hello {name}!";
}
```

In most cases, an orchestrator function will actually call another function as part of its implementation, so here is another "Hello World" example which demonstrates this:

```csharp
[FunctionName("HelloWorld")]
public static async Task<string> Run(
    [OrchestrationTrigger] DurableOrchestrationContext context)
{
    string name = await context.GetInput<string>();
    string result = await context.CallActivityAsync<string>("SayHello", name);
    return result;
}
```

## Activity triggers
The activity trigger enables you to author functions which can be called by orchestrator functions.

When using the Visual Studio tools for Azure Functions, the activity trigger is configured using the <xref:Microsoft.Azure.WebJobs.ActivityTriggerAttribute> .NET attribute. 

When using the Azure Portal for development, the activity trigger is defined by the following JSON object in the `bindings` array of function.json:

```json
{
    "name": "<Name of input parameter in function signature>",
    "activity": "<Optional - name of the activity>",
    "version": "<Optional - version label of this activity function>",
    "type": "activityTrigger",
    "direction": "in"
}
```

Note the following:
* `activity` is the name of the activity. This is the value that orchestrator functions must use when they want to invoke this activity function. This property is optional. If not specified, the name of the function is used.
* `version` is a version label of the activity. Orchestrator functions that invoke an activity must include the matching version label. This property is optional. If not specified, the empty string is used. For more information on versioning, see the [Versioning](./versioning.md) topic.

Internally this trigger binding polls a queue in the default storage account for the function app. This queue is an internal implementation detail of the extension, which is why it is not explicitly configured in the binding properties.

> [!NOTE]
> Setting values for `activity` or `version` properties is not recommended at this time.

### Trigger behavior
Note the following behaviors of the activity trigger:
* **threading** - unlike the orchestration trigger, activity triggers don't have any restrictions around threading or I/O. They can be treated like regular functions.
* **Poising-message handling** - there is no poison message support in activity triggers.
* **Message visibility** - activity trigger messages are dequeued and kept invisible for a configurable duration. The visibility of these messages is renewed automatically as long as the function app is running and healthy.
* **Return values** - return values are serialized to JSON and persisted to the orchestration history table in Azure Table storage.

> [!WARNING]
> The storage backend for activity functions is an implementation detail and user code should not interact with these storage entities directly.

### Trigger usage
The activity trigger binding supports both inputs and outputs, just like the orchestration trigger. Here are some things to know about input and output handling:

* **inputs** - activity functions natively use <xref:Microsoft.Azure.WebJobs.DurableActivityContext> as a parameter type. Alternatively, an activity function can be declared with any parameter type that is JSON-serializable. When using `DurableActivityContext`, code can use <xref:Microsoft.Azure.WebJobs.DurableActivityContext.GetInput*> to fetch and deserialize the activity function input.
* **outputs** - activity triggers support output values as well as inputs. The return value of the function is used to assign the output value and must be JSON-serializable. If a function returns `Task` or `void` then a `null` value will be saved as the output.
* **metadata** - activity functions can also bind to a `string instanceId` parameter to get the instance ID of the parent orchestration.

> [!NOTE]
> Activity triggers are not currently supported in Node.js functions.

### Trigger sample
The following is an example of what a very simple "Hello World" C# activity function might look like:

```csharp
[FunctionName("SayHello")]
public static string SayHello([ActivityTrigger] DurableActivityContext helloContext)
{
    string name = helloContext.GetInput<string>();
    return $"Hello {name}!";
}
```

The default parameter type for the <xref:Microsoft.Azure.WebJobs.ActivityTriggerAttribute> binding is <xref:Microsoft.Azure.WebJobs.DurableActivityContext>. However, activity triggers also support binding directly to JSON-serializeable types (including primitive types), so the same function could be simplified as follows:

```csharp
[FunctionName("SayHello")]
public static string SayHello([ActivityTrigger] string name)
{
    return $"Hello {name}!";
}
```

## Orchestration client
The orchestration client binding enables you to write functions which interact with orchestrator functions. This includes starting new orchestration instances, querying their status, terminating, and sending events to running orchestration instances.

When using the Visual Studio tools for Azure Functions, the orchestration client can be bound to using the <xref:Microsoft.Azure.WebJobs.OrchestrationClientAttribute> .NET attribute.

When using scripting languages (e.g. .csx files) for development, the orchestration trigger is defined by the following JSON object in the `bindings` array of function.json:

```json
{
    "name": "<Name of input parameter in function signature>",
    "taskHub": "<Optional - name of the task hub>",
    "connectionName": "<Optional - name of the connection string app setting>",
    "type": "orchestrationClient",
    "direction": "out"
}
```

Note the following:
* The `taskHub` property is used in scenarios where multiple function apps share the same storage account but need to be isolated from each other. If not specified, the default value from `host.json` is used. This value must match the value used by the target orchestrator functions.
* The `connectionName` property must contain the name of an app setting that contains a storage connection string. The storage account represented by this connection string must be the same one used by the target orchestrator functions. If not specified, the default connection string for the function app is used.

> [!NOTE]
> In most cases, it is recommended to omit these properties and rely on the default behavior.

### Client usage
In C# functions, you typically bind to <xref:Microsoft.Azure.WebJobs.DurableOrchestrationClient>, which gives you full access to all client APIs supported by Durable Functions. APIs on the client object include:

* <xref:Microsoft.Azure.WebJobs.DurableOrchestrationClient.StartNewAsync*>
* <xref:Microsoft.Azure.WebJobs.DurableOrchestrationClient.GetStatusAsync*>
* <xref:Microsoft.Azure.WebJobs.DurableOrchestrationClient.TerminateAsync*>
* <xref:Microsoft.Azure.WebJobs.DurableOrchestrationClient.RaiseEventAsync*>

Alternatively, you can bind to `IAsyncCollector<T>` where `T` is one of <xref:Microsoft.Azure.WebJobs.StartOrchestrationArgs> or `JObject`.

See the <xref:Microsoft.Azure.WebJobs.DurableOrchestrationClient> API documentation for additional details on these operations.

### Client sample (Visual Studio Development)
Here is an example queue-triggered function which starts a "HelloWorld" orchestration.

```csharp
[FunctionName("QueueStart")]
public static Task Run(
    [QueueTrigger("durable-function-trigger")] string input,
    [OrchestrationClient] DurableOrchestrationClient starter)
{
    // Orchestration input comes from the queue message content.
    return starter.StartNewAsync("HelloWorld", input);
}
```

### Client sample (Non-Visual Studio)
If you're not using Visual Studio for development, you can create the following function.json file, which shows how to configure a queue-triggered function that uses the durable orchestration client binding:

```json
{
  "bindings": [
    {
      "name": "input",
      "type": "queueTrigger",
      "queueName": "durable-function-trigger",
      "direction": "in"
    },
    {
      "name": "starter",
      "type": "orchestrationClient",
      "direction": "out"
    }
  ],
  "disabled": false
} 
```

Below are language-specific samples that start new orchestrator function instances.

#### C# Sample
The following sample shows how to use the durable orchestration client binding to start a new function instance from a C# script function:

```csharp
#r "Microsoft.Azure.WebJobs.Extensions.DurableTask"

public static Task<string> Run(string input, DurableOrchestrationClient starter)
{
    return starter.StartNewAsync("HelloWorld", input);
}
```

#### Node.js Sample
The following sample shows how to use the durable orchestration client binding to start a new function instance from a Node.js function:

```js
module.exports = function (context, input) {
    var id = generateSomeUniqueId();
    context.bindings.starter = [{
        FunctionName: "HelloWorld",
        Input: input,
        InstanceId: id
    }];

    context.done(null, id);
};
```
More details on starting instances can be found in the [Instance Management](./instance-management.md) topic.