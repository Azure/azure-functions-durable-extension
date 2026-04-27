# Work Item Filters Sample

This sample demonstrates the **work item filtering** feature for Durable Functions with the [Durable Task Scheduler](https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-task-hubs?tabs=csharp#durable-task-scheduler) (DTS) backend.

## What are work item filters?

When multiple Function apps share the same DTS task hub, the backend dispatches work items (orchestrations, activities, entities) to any connected worker. This can cause failures when a worker receives a function it doesn't have:

> *"The function 'X' doesn't exist, is disabled, or is not an orchestrator function."*

Work item filters solve this by having each app automatically advertise which functions it handles. DTS then only dispatches matching work to each app.

## How it works

1. The Durable Functions extension discovers registered orchestrators, activities, and entities during function indexing
2. These names are sent to the DTS backend as `WorkItemFilters` on the `GetWorkItems` gRPC stream
3. DTS only dispatches work items that match the worker's registered functions
4. Unmatched work items stay in the queue until a worker with the right filter connects

No code changes are required — just enable the feature in `host.json`.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for Azurite and optionally the DTS emulator)
- A running Durable Task Scheduler instance (see [Setup DTS](#setup-dts) below)

## Setup DTS

### Option A: Docker emulator (recommended for local dev)

```bash
docker run -d --name dts-emulator -p 8080:8080 mcr.microsoft.com/dts/emulator:latest
```

### Option B: Build from source

If you have the DTS backend source code:

```bash
dotnet run --project src/Backend/Microsoft.DTMB.BackendAPI \
  -p:DefineConstants=EMULATOR_BUILD \
  -- --Database:UseDatabase=false \
     --ClientAuth:DisableAuthentication=true \
     --Database:TaskHubNames=default \
     --urls="http://localhost:8080;http://localhost:8081"
```

## Setup Azurite (storage emulator)

```bash
docker run -d --name azurite -p 10000:10000 -p 10001:10001 -p 10002:10002 \
  mcr.microsoft.com/azure-storage/azurite
```

## Configuration

The sample's [`host.json`](host.json) enables work item filtering:

```json
{
  "extensions": {
    "durableTask": {
      "storageProvider": {
        "type": "azureManaged",
        "connectionStringName": "DURABLE_TASK_SCHEDULER_CONNECTION_STRING",
        "workItemFilteringEnabled": true
      }
    }
  }
}
```

The [`local.settings.json`](local.settings.json) points to the local DTS emulator:

```json
{
  "Values": {
    "DURABLE_TASK_SCHEDULER_CONNECTION_STRING": "Endpoint=http://localhost:8080;Authentication=None"
  }
}
```

## Run the sample

```bash
cd samples/workitem-filters
func start --port 7071
```

You should see log output like:

```
Work item filtering enabled. Registered 4 orchestrators, 1 activities, 1 entities.
```

## Test the sample

Open [`demo.http`](demo.http) in VS Code with the [REST Client extension](https://marketplace.visualstudio.com/items?itemName=humao.REST-Client) and step through the requests in order.

Alternatively, use curl:

```bash
# 1. Start an orchestration (should complete)
curl -X POST http://localhost:7071/api/orchestrators/greeting

# 2. Check status (copy instanceId from response)
curl http://localhost:7071/api/instances/<instanceId>

# 3. Schedule an unknown orchestration (should stay Pending with filters)
curl -X POST http://localhost:7071/api/start/SomeOtherOrchestration

# 4. Check status — should be Pending, proving filter isolation
curl http://localhost:7071/api/instances/<instanceId>
```

## What this sample registers

| Type          | Function Name           | Description                             |
|---------------|-------------------------|-----------------------------------------|
| Orchestration | `GreetingOrchestration` | Simple activity call                    |
| Orchestration | `FanOutOrchestration`   | Parallel fan-out to 3 activity calls    |
| Orchestration | `ParentOrchestration`   | Calls `GreetingOrchestration` as a sub-orchestration |
| Orchestration | `CounterOrchestration`  | Interacts with `CounterEntity`          |
| Activity      | `SayHello`              | Returns a greeting string               |
| Entity        | `CounterEntity`         | Simple counter with Add/Reset/Get       |

With filtering enabled, DTS will **only** dispatch these types to this worker. Scheduling any other orchestration name (e.g., `SomeOtherOrchestration`) via the generic starter will result in it staying `Pending` — proving that DTS is correctly holding unmatched work items.

## Multi-app scenario

To demonstrate filter isolation across two apps:

1. Create a second Function app with **different** orchestrations/activities/entities
2. Point both apps to the **same** DTS task hub (same connection string and hub name)
3. Enable `workItemFilteringEnabled: true` in both apps' `host.json`
4. Schedule orchestrations from either app — each will only process its own functions

## Key behaviors

| Scenario | Behavior |
|----------|----------|
| Work item matches a registered function | Dispatched to this worker |
| Work item does NOT match any registered function | Held in DTS queue (not dispatched) |
| `workItemFilteringEnabled` is `false` or not set | All work items dispatched to all workers (default, no filtering) |
| Worker disconnects | Filter channel stays active briefly; items drain back to the general queue after a timeout |
