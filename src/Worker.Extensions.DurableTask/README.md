# Microsoft.Azure.Functions.Worker.Extensions.DurableTask

This is the **Durable Functions extension package for .NET isolated** (out-of-process) function apps. It defines worker-specific trigger and binding attributes (`[OrchestrationTrigger]`, `[ActivityTrigger]`, `[EntityTrigger]`, `[DurableClient]`) and a middleware-based execution shim, but all durable state management, replay, and scheduling is handled by the host-side extension (`Microsoft.Azure.WebJobs.Extensions.DurableTask`). This package is built on top of the [Durable Task .NET SDK](https://github.com/microsoft/durabletask-dotnet) (`durabletask-dotnet`), which provides the client and worker abstractions used to communicate with the host over a local gRPC channel.

| | |
|---|---|
| **NuGet Package** | [`Microsoft.Azure.Functions.Worker.Extensions.DurableTask`](https://www.nuget.org/packages/Microsoft.Azure.Functions.Worker.Extensions.DurableTask) |
| **Target Frameworks** | `netstandard2.0`, `net8.0`, `net10.0` |

## Where This Code Runs

This extension runs **inside the .NET isolated worker process**. It is automatically registered via `[WorkerExtensionStartup]`, which the worker SDK discovers at startup.

At runtime, the extension uses the [Durable Task .NET SDK](https://github.com/microsoft/durabletask-dotnet) (`durabletask-dotnet`) to connect to the **host-side gRPC sidecar** (provided by `WebJobs.Extensions.DurableTask`) for gRPC communication, orchestration replay, and activity/entity dispatch. Specifically, it uses `Microsoft.DurableTask.Worker.Grpc` for running orchestrator and entity replay in the worker process, and `Microsoft.DurableTask.Client.Grpc` for client operations like starting orchestrations and querying instance status. The host extension manages all durable state and storage backend interaction.

## Key Dependencies

| Package | Details |
|---|---|
| [`Microsoft.DurableTask.Client.Grpc`](https://github.com/microsoft/durabletask-dotnet) | The gRPC-based Durable Task client SDK. |
| [`Microsoft.DurableTask.Worker.Grpc`](https://github.com/microsoft/durabletask-dotnet) | The gRPC-based Durable Task worker SDK that handles orchestration replay and entity dispatch. |
| [`Microsoft.Azure.Functions.Worker.Core`](https://github.com/dotnet/azure-functions-dotnet-worker) | Core abstractions for the .NET isolated worker model. |
| [`Microsoft.Azure.Functions.Worker.Extensions.Abstractions`](https://github.com/dotnet/azure-functions-dotnet-worker) | Base classes for worker extension attributes. |
