# Task Hubs
A task hub is a logical container for durable task orchestrations and activities within the context of a single Azure storage account. Multiple functions and even function apps can exist in the same task hub, and the task hub often serves as an application container.

Task hubs do not need to be created explicitly. Rather, they are declared by name in **host.json** and initialized automatically by the runtime. Each task hub has its own set of storage queues, tables, and blobs within a single storage account, and all function apps which run in a given task hub will share the same storage resources. Orchestrator and activity functions can only interact with each other when they belong to the same task hub.

## Configuring a Task Hub in host.json
A task hub name can be configured in the **host.json** file of a function app.

```json
{
  "durableTask": {
    "HubName": "MyTaskHub"
  }
}
```

Task hub names must start with a letter and consist of only letters and numbers. If not specified, the default task hub name for a function app is **DurableFunctionsHub**.

> [!NOTE]
> If you have multiple function apps which share a storage account, it is recommended to configure a different task hub name for each function app. This ensures that each function app is properly isolated from each other.

## Azure Storage resources
A task hub consists of several Azure Storage resources:

* One or more control queues.
* One work-item queue.
* One history table.
* One storage container containing one or more lease blobs.

All of these resources are created automatically in the default Azure Storage account when orchestrator or activity functions run or are scheduled to run.

> [!NOTE]
> For more information on how task hub storage resources are used to scale out durable functions, see the [Performance & Scale](~/articles/topics/perf-and-scale.md) topic.
