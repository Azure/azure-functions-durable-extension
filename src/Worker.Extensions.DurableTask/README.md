# Microsoft.Azure.Functions.Worker.Extensions.DurableTask

This is the **worker-side SDK for Durable Functions on the .NET isolated worker** (out-of-process) hosting model. It defines worker-specific trigger and binding attributes (`[OrchestrationTrigger]`, `[ActivityTrigger]`, `[EntityTrigger]`, `[DurableClient]`) and a middleware-based execution shim, but all durable state management, replay, and scheduling is handled by the host-side extension (`WebJobs.Extensions.DurableTask`). This package is built on top of the [Durable Task .NET SDK](https://github.com/microsoft/durabletask-dotnet) (`durabletask-dotnet`), which provides the client and worker abstractions used to communicate with the host over a local gRPC channel.

| | |
|---|---|
| **NuGet Package** | [`Microsoft.Azure.Functions.Worker.Extensions.DurableTask`](https://www.nuget.org/packages/Microsoft.Azure.Functions.Worker.Extensions.DurableTask) |
| **Target Frameworks** | `netstandard2.0`, `net8.0`, `net10.0` |

## Where This Code Runs

This extension runs **inside the .NET isolated worker process**. It is automatically registered via `[WorkerExtensionStartup]`, which the worker SDK discovers at startup.

At runtime, the extension connects to the **host-side gRPC sidecar** (provided by `WebJobs.Extensions.DurableTask`) over `127.0.0.1` to send and receive orchestration commands, activity results, and entity operations. The host extension manages all durable state; this worker extension is a thin client and execution shim.

## Key Dependencies

| Package | Details |
|---|---|
| [`Microsoft.DurableTask.Client.Grpc`](https://github.com/microsoft/durabletask-dotnet) | The gRPC-based Durable Task client SDK. |
| [`Microsoft.DurableTask.Worker.Grpc`](https://github.com/microsoft/durabletask-dotnet) | The gRPC-based Durable Task worker SDK that handles orchestration replay. |
| `Microsoft.Azure.Functions.Worker.Core` | Core abstractions for the .NET isolated worker model. |
| `Microsoft.Azure.Functions.Worker.Extensions.Abstractions` | Base classes for worker extension attributes. |
