# Bindings
## Overview
The Durable Functions extension introduces two new trigger bindings which control the execution of orchestrator and activity functions. It also introduces an output binding which acts as a client into the Durable Functions runtime.

## Orchestration triggers
The orchestration trigger enables you to author durable orchestrator functions. This trigger supports starting new orchestrator function instances and resuming existing orchestrator function instances that are "awaiting" on a task.

The orchestration trigger is defined by the following JSON object in the `bindings` array of function.json:

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
* `version` is a version label of the orchestration. Clients that start a new instance of an orchestration must include the matching version label. This property is optional. If not specified, the empty string is used. For more information on versioning, see the [Versioning](./versioning.md) topic (coming soon!).

Internally this trigger binding polls a queue in the default storage account for the function app. The queue itself is an internal implementation detail, which is why it is not explicitly configured in the binding properties.

> [!NOTE]
> Setting values for `orchestration` or `version` properties is not recommended at this time. These are advanced settings and are generally not needed except in versioning scenarios, which have not yet received much testing.

### Trigger behavior
Note the following behaviors of the orchestration trigger:
* **single-threading** - a single dispatcher thread is used for all orchestrator function execution. For this reason, it is important to ensure that orchestrator function code is efficient and doesn't perform any I/O. It is also important that this thread does not do any async work except when awaiting on Durable Functions-specific task types.
* **Poising-message handling** - there is no poison message support in orchestration triggers.
* **Message visibility** - orchestration trigger messages are dequeued and kept invisible for a configurable duration. The visibility of these messages is renewed automatically as long as the function app is running and healthy.
* **Return values** - return values are serialized to JSON and persisted to the orchestration history table in Azure Table storage. These return values can be queried by the orchestration client binding, described later.

> [!WARNING]
> Orchestration functions must never use any input or output bindings (other than the orchestration trigger binding). Doing so has the potential to break the orchestration function dispatcher because those bindings may not obey the single-threading and I/O rules.

> [!WARNING]
> The storage backend for orchestration functions is an implementation detail and user code should not interact with these storage entities directly.

### Trigger usage
The orchestration trigger binding supports both inputs and outputs. Here are some things to know about input and output handling:

* **inputs** - orchestration functions only support using <xref:Microsoft.Azure.WebJobs.DurableOrchestrationContext> as a parameter type. Deserialization inputs directly in the function signature is not yet supported. Code must use `DurableOrchestrationContext.GetInput<T>()` to fetch orchestrator function inputs. These inputs must be JSON-serializable types.
* **outputs** - Orchestration triggers support output values as well as inputs. Output values can be assigned using a return value or using the `DurableOrchestrationContext.SetOutput(object)` method. Outputs must be JSON-serializable types.

> [!NOTE]
> Orchestration triggers are not currently supported in Node.js functions.

### Trigger sample
Suppose you have the following function.json (most orchestrator functions will look exactly like this):

```json
{
  "bindings": [
    {
      "name": "context",
      "type": "orchestrationTrigger",
      "direction": "in"
    }
  ],
  "disabled": false
}
```

The following is an example of how a corresponding "Hello World" C# orchestrator function might look like:

```csharp
#r "Microsoft.Azure.WebJobs.Extensions.DurableTask"

public static string Run(DurableOrchestrationContext context)
{
    string name = context.GetInput<string>();
    return $"Hello {name}!";
}
```

In most cases, an orchestrator function will actually call another function as part of its implementation, so here is another "Hello World" example which demonstrates this:

```csharp
#r "Microsoft.Azure.WebJobs.Extensions.DurableTask"

public static async Task<string> Run(DurableOrchestrationContext context)
{
    string name = await context.GetInput<string>();
    string result = await context.CallFunctionAsync<string>("SayHello", name);
    return result;
}
```

## Activity triggers
The activity trigger enables you to author functions which can be called by orchestrator functions.

The activity trigger is defined by the following JSON object in the `bindings` array of function.json:

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
* `version` is a version label of the activity. Orchestrator functions that invoke an activity must include the matching version label. This property is optional. If not specified, the empty string is used. For more information on versioning, see the [Versioning](./versioning.md) topic (coming soon!).

Internally this trigger binding polls a queue in the default storage account for the function app. The queue itself is an internal implementation detail, which is why it is not explicitly configured in the binding properties.

> [!NOTE]
> Setting values for `activity` or `version` properties is not recommended at this time. These are advanced settings and are generally not needed except in versioning scenarios, which have not yet received much testing.

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

* **inputs** - activity functions must use <xref:Microsoft.Azure.WebJobs.DurableActivityContext> as a parameter type. Deserialization inputs directly in the function signature is not yet supported. Code can use <xref:Microsoft.Azure.WebJobs.DurableActivityContext.GetInput``1> to fetch activity function inputs. These inputs must be JSON-serializable types.
* **outputs** - activity triggers support output values as well as inputs. Output values can be assigned using a return value or using the <xref:Microsoft.Azure.WebJobs.DurableActivityContext.SetOutput*> method. Outputs must be JSON-serializable types.
* **metadata** - activity functions can also bind to a `string instanceId` parameter to get the instance ID of the parent orchestration.

> [!NOTE]
> Activity triggers are not currently supported in Node.js functions.

### Trigger sample
Suppose you have the following function.json:

```json
{
  "bindings": [
    {
      "name": "context",
      "type": "activityTrigger",
      "direction": "in"
    }
  ],
  "disabled": false
}
```

The following is an example of how a corresponding "Hello World" C# activity function might look like:

```csharp
#r "Microsoft.Azure.WebJobs.Extensions.DurableTask"

public static string Run(DurableActivityContext context)
{
    string name = context.GetInput<string>();
    return $"Hello {name}!";
}
```

## Orchestration client
The orchestration client binding enables you to write functions which interact with orchestrator functions. This includes starting new orchestration instances, querying their status, terminating, and sending events to running orchestration instances.

The orchestration trigger is defined by the following JSON object in the `bindings` array of function.json:

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
> In most cases, it is recommended to not configure these values explicitly.

### Output usage
In C# functions, you typically bind to <xref:Microsoft.Azure.WebJobs.DurableOrchestrationClient>, which gives you full access to all client APIs supported by Durable Functions. APIs on the client object include:

* <xref:Microsoft.Azure.WebJobs.DurableOrchestrationClient.StartNewAsync*>
* <xref:Microsoft.Azure.WebJobs.DurableOrchestrationClient.GetStatusAsync*>
* <xref:Microsoft.Azure.WebJobs.DurableOrchestrationClient.TerminateAsync*>
* <xref:Microsoft.Azure.WebJobs.DurableOrchestrationClient.RaiseEventAsync*>

Alternatively, you can bind to `IAsyncCollector<T>` where `T` is one of <xref:Microsoft.Azure.WebJobs.StartOrchestrationArgs> or `JObject`.

See the <xref:Microsoft.Azure.WebJobs.DurableOrchestrationClient> API documentation for additional details on these operations.

### Output sample
Suppose you have the following function.json, which shows how to configure a queue-triggered function that uses the durable orchestration client binding:

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
The following sample shows how to use the durable orchestration client binding to start a new function instance from a C# function:

```csharp
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