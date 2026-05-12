# Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers

This is a **Roslyn analyzer** package that provides compile-time diagnostics and code fixes for common issues when writing Durable Functions code. It targets the **.NET in-process** model only. Isolated worker (.NET out-of-process) users get a separate set of analyzers from the [`durabletask-dotnet`](https://github.com/microsoft/durabletask-dotnet) SDK instead.

> [!NOTE]
> This package is no longer being actively maintained. It is only relevant to the [.NET in-process model](https://learn.microsoft.com/en-us/azure/azure-functions/functions-dotnet-class-library?tabs=v4%2Ccmd), which is being deprecated in favor of the [.NET isolated worker model](https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide).

The analyzer helps developers catch bugs early — particularly violations of the [orchestrator code constraints](https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-code-constraints) that can cause non-deterministic replay failures.

| | |
|---|---|
| **NuGet Package** | [`Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers`](https://www.nuget.org/packages/Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers) |
| **Target Framework** | `netstandard2.0` |

## Where This Code Runs

This code runs **inside the C# compiler process** (Roslyn) at build time and inside the IDE (Visual Studio, VS Code with C# Dev Kit) for real-time diagnostics. It does **not** run at runtime in Azure Functions — it is strictly a development-time tool.

The analyzer DLL is packaged into the NuGet package's `analyzers/dotnet/cs` folder so that it is automatically loaded by the compiler when the package is referenced. It is also included as a dependency of the main `Microsoft.Azure.WebJobs.Extensions.DurableTask` package, so in-process C# users get these diagnostics automatically.

## What It Checks

The analyzers cover orchestrator determinism constraints (e.g., flagging `DateTime.Now`, `Thread.Sleep`, direct I/O), activity function signatures, entity definitions, and binding usage. The `CodefixProviders/` folder contains automated code fixes for many of these diagnostics.

## Key Dependencies

| Package | Details |
|---|---|
| `Microsoft.CodeAnalysis.CSharp.Workspaces` | Roslyn APIs for syntax analysis and code fix authoring. |
| `Microsoft.CodeAnalysis.Analyzers` | Meta-analyzers that validate analyzer implementations. |
