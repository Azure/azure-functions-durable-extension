# Microsoft.Azure.WebJobs.Extensions.DurableTask

This is the core extension that **powers Durable Functions** on the Azure Functions host. It provides orchestration, activity, and entity trigger bindings, the durable client output binding, HTTP and gRPC management APIs, and storage backend integration. For **.NET in-process** customers, this is the NuGet package they directly reference to write Durable Functions. It also serves as the **host-side component** for all out-of-process (isolated worker) language SDKs (.NET isolated, JavaScript, Python, PowerShell, Java).

| | |
|---|---|
| **NuGet Package** | [`Microsoft.Azure.WebJobs.Extensions.DurableTask`](https://www.nuget.org/packages/Microsoft.Azure.WebJobs.Extensions.DurableTask) |
| **Target Frameworks** | `net8.0`, `net10.0` |

## Where This Code Runs

This extension is loaded **into the Azure Functions host process** and is registered via `[WebJobsStartup]`, so the WebJobs SDK discovers and initializes it automatically at host startup.

When used with out-of-process language SDKs, this extension still runs inside the host. It communicates with the language worker via a **local gRPC sidecar** that the worker SDK connects to. The `.proto` definitions are vendored from [durabletask-protobuf](https://github.com/microsoft/durabletask-protobuf) — see `Grpc/Protos/README.md` for update instructions.

## Bindings

The extension implements `IExtensionConfigProvider` to integrate with the Azure Functions binding system. It registers trigger bindings for orchestrations, activities, and entities, plus an input/output binding for the durable client. These bindings are what let users write `[OrchestrationTrigger]`, `[ActivityTrigger]`, `[EntityTrigger]`, and `[DurableClient]` in their function signatures. See the `Bindings/` and `TriggerAttributes/` folders for the implementation.

## HTTP Management API

`HttpApiHandler` exposes REST endpoints for starting orchestrations, querying instance status, raising events, managing instance lifecycle (terminate, suspend, resume, etc.), and interacting with entities. The Azure Functions host routes webhook traffic into `DurableTaskExtension`, which delegates to `HttpApiHandler.HandleRequestAsync`.

## Relationship with DurableTask.Core (DTFx)

This extension is a **hosting layer on top of the [Durable Task Framework](https://github.com/Azure/durabletask)** (DTFx, `Microsoft.Azure.DurableTask.Core`). DTFx owns the orchestration state machine, replay engine, and work-item scheduling. This extension builds on top of it to provide the Azure Functions binding model, HTTP management API, gRPC sidecar for out-of-process workers, entity programming model, and telemetry integration.

## Storage Backends

Azure Storage is the default backend for Durable Functions. The `DurabilityProvider` abstraction allows plugging in alternative storage backends:

| Backend | Repository |
|---|---|
| **Azure Storage** (default) | [`Azure/durabletask`](https://github.com/Azure/durabletask) |
| **Netherite** | [`microsoft/durabletask-netherite`](https://github.com/microsoft/durabletask-netherite) |
| **MSSQL** | [`microsoft/durabletask-mssql`](https://github.com/microsoft/durabletask-mssql) |
| **Durable Task Scheduler** | [`Azure-Samples/Durable-Task-Scheduler`](https://github.com/Azure-Samples/Durable-Task-Scheduler) *(samples only)* |

The `Scale/` folder provides **runtime scaling support**, integrated via `DurableTaskListener`.

## Extension Bundle Compatibility

This extension is distributed as part of [Azure Functions extension bundles](https://learn.microsoft.com/azure/azure-functions/functions-bindings-register#extension-bundles), which are used by non-.NET language SDKs. Changes to public APIs and package dependencies must remain compatible with the extension bundle dependency graph. Breaking changes to package references can cause runtime failures for all non-.NET Durable Functions users.

## Key Dependencies

| Package | Details |
|---|---|
| [`Microsoft.Azure.DurableTask.Core`](https://github.com/Azure/durabletask) | The underlying Durable Task Framework that provides orchestration replay, work-item dispatch, and history management. |
| [`Microsoft.Azure.DurableTask.AzureStorage`](https://github.com/Azure/durabletask) | The default Azure Storage backend for Durable Functions. |
| `Microsoft.Azure.WebJobs` | The Azure WebJobs SDK that this extension plugs into. |
| `Grpc.Tools` | Build-time proto compilation for the gRPC sidecar. |
