# Copilot Instructions — Azure Functions Durable Extension

This document provides architectural context for AI assistants working with this codebase.
Focus is on **stable patterns, invariants, and pitfalls** — not file paths or function signatures.

---

## What This Project Is

The **Azure Functions Durable Extension** (a.k.a. Durable Functions) is the official
Azure Functions extension that enables writing stateful, long-running workflows as code.
It provides orchestrator functions, activity functions, and entity functions on top of
the Durable Task Framework.

This repo contains two main extension packages:

| Package | NuGet | Description |
|---|---|---|
| `Microsoft.Azure.WebJobs.Extensions.DurableTask` | In-process (.NET) | Classic WebJobs-based extension for Azure Functions v1–v4 in-process |
| `Microsoft.Azure.Functions.Worker.Extensions.DurableTask` | Isolated (.NET worker) | Extension for the .NET isolated worker model |

It also includes a **Roslyn analyzer** package that detects common Durable Functions
coding mistakes at compile time.

**This is NOT the standalone Durable Task SDK.** That lives in separate repos
(`durabletask-dotnet`, `durabletask-js`, etc.). This extension *consumes* the
Durable Task Framework and adds Azure Functions trigger bindings, middleware, and
Azure Storage / Netherite / MSSQL backend integration.

---

## Core Execution Model — Replay-Based Orchestrations

### How Orchestrations Work

Orchestrator functions are replayed from history to rebuild state. The extension manages
the replay loop, event sourcing, and communication with the chosen storage backend
(Azure Storage, Netherite, or MSSQL).

On every re-invocation:
1. The orchestrator function receives its `IDurableOrchestrationContext`.
2. Completed tasks resolve instantly from history (replay).
3. Incomplete tasks suspend the orchestrator until the next event arrives.
4. New actions are written to the storage backend.

### The Determinism Rule (Critical)

**Orchestrator code MUST be deterministic.** Every replay must produce the exact same
sequence of actions. Violations cause `NonDeterministicOrchestrationException`.

What this means in practice:
- **No `DateTime.Now`** — use `context.CurrentUtcDateTime`
- **No `Guid.NewGuid()`** — use `context.NewGuid()`
- **No direct I/O** (HTTP calls, file reads, database queries) — use activities
- **No `Task.Delay`** — use `context.CreateTimer()`
- **No thread-unsafe or environment-dependent operations**

### Activities vs Orchestrations

Activities execute side effects exactly once (modulo retries) and persist results
as history events. They can do anything: HTTP calls, DB writes, etc.

**Key mental model:** Orchestrations = coordination logic (deterministic).
Activities = real work (non-deterministic allowed).

### Entities

Durable Entities provide stateful, actor-like programming. They process operations
sequentially and can be signaled or called from orchestrations or clients.

---

## Architecture — How Pieces Connect

### In-Process Extension (`WebJobs.Extensions.DurableTask`)

The in-process extension hooks into the Azure Functions WebJobs SDK:
- `DurableTaskExtension` — the main extension entry point (implements `IExtensionConfigProvider`)
- `DurableOrchestrationContext` — wraps the Durable Task Framework's orchestration context
- `DurableActivityContext` / `DurableEntityContext` — similar wrappers for activities/entities
- Trigger bindings: `OrchestrationTriggerAttribute`, `ActivityTriggerAttribute`, `EntityTriggerAttribute`
- Client bindings: `DurableClientAttribute`

### Isolated Worker Extension (`Worker.Extensions.DurableTask`)

The isolated worker extension uses gRPC to communicate between the Functions host
and the worker process:
- `DurableTaskFunctionsMiddleware` — middleware for the isolated worker
- Protobuf messages for cross-process orchestration replay
- Client, orchestration, and entity abstractions that proxy through gRPC

### Storage Backends

The extension supports multiple storage providers:
- **Azure Storage** (default) — uses Azure Table Storage, Blob Storage, and Queue Storage
- **Netherite** — high-performance backend using Event Hubs and FASTER
- **MSSQL** — Microsoft SQL Server backend

### Roslyn Analyzers

The analyzer package (`WebJobs.Extensions.DurableTask.Analyzers`) provides compile-time
checks for common mistakes:
- Non-deterministic API usage in orchestrators
- Incorrect binding attribute usage
- Invalid orchestrator patterns

---

## Project Structure

```
src/
  WebJobs.Extensions.DurableTask/          # In-process extension
  Worker.Extensions.DurableTask/           # Isolated worker extension
  WebJobs.Extensions.DurableTask.Analyzers/# Roslyn analyzers
  DurableFunctions.TypedInterfaces/        # Code-gen for typed interfaces

test/
  FunctionsV1/                             # Tests targeting Functions v1
  FunctionsV2/                             # Tests targeting Functions v2+ (main test project)
  Common/                                  # Shared test code
  Worker.Extensions.DurableTask.Tests/     # Worker extension unit tests
  WebJobs.Extensions.DurableTask.Analyzers.Test/ # Analyzer tests
  e2e/                                     # End-to-end test apps
  SmokeTests/                              # Smoke tests for various runtimes
  DFPerfScenarios/                         # Performance test scenarios

samples/                                   # Sample applications

docs/                                      # Documentation
```

---

## Build and Test

### Building

```bash
dotnet restore WebJobs.Extensions.DurableTask.sln
dotnet build WebJobs.Extensions.DurableTask.sln
```

### Running Tests

Tests require **Azurite** (Azure Storage emulator) running on ports 10000/10001/10002:

```bash
# Start Azurite (or use npm: npx azurite)
docker run -d -p 10000:10000 -p 10001:10001 -p 10002:10002 \
  mcr.microsoft.com/azure-storage/azurite

# Run in-process extension tests (excluding E2E)
dotnet test ./test/FunctionsV2/WebJobs.Extensions.DurableTask.Tests.V2.csproj \
  --filter "FullyQualifiedName!~DurableTaskEndToEndTests"

# Run worker extension tests
dotnet test ./test/Worker.Extensions.DurableTask.Tests/Worker.Extensions.DurableTask.Tests.csproj

# Run analyzer tests
dotnet test ./test/WebJobs.Extensions.DurableTask.Analyzers.Test/WebJobs.Extensions.DurableTask.Analyzers.Test.csproj
```

---

## Error Handling Patterns

| Error | When |
|---|---|
| `FunctionFailedException` | Activity or sub-orchestration fails |
| `NonDeterministicOrchestrationException` | Replayed action mismatches history |
| `TimeoutException` | Durable timer exceeds timeout |
| `TaskFailedException` | Activity throws during execution |

---

## Code Conventions

### C# Style
- Top of all `.cs` files: Microsoft copyright header + MIT license reference
- All public methods and classes must have XML documentation comments
- Use `this.` for accessing class members
- Use `Async` suffix on async method names
- Private classes that don't serve as base classes must be `sealed`
- StyleCop analyzers enforce formatting rules

### Testing
- Framework: **xUnit** with **Moq** for mocking
- Test projects use shared code from `test/Common/`
- E2E tests are separate from unit tests and may require live Azure resources

### Branching
- **`dev`** — default branch; PRs target `dev`
- **`main`** — secondary branch
- **`v3.x`** — maintenance branch for v3

### Breaking Changes
- Changes should not introduce breaking changes unless explicitly noted
- Version updates to the in-process extension must be reflected in
  `Worker.Extensions.DurableTask/AssemblyInfo.cs`

---

## What Not to Touch

- **`sign.snk`** — strong naming key file
- **`nuget.config`** — NuGet configuration
- **Version fields** in `.csproj` files unless doing an intentional version bump
- **Generated code** from the TypedInterfaces source generator

---

## Key Design Constraints

1. **Multi-target framework support** — the in-process extension targets `netstandard2.0`,
   `netcoreapp3.1`, and `net462`
2. **Wire-compatibility** — changes must not break existing orchestrations in flight
3. **Cross-language impact** — the in-process extension is the host-side component for
   all language SDKs (JS, Python, Java, PowerShell) — changes affect all languages
4. **Backward compatibility** — must support Azure Functions runtime v1 through v4
5. **Performance sensitivity** — the extension runs in the hot path of every orchestration
   replay; allocations and blocking calls matter
