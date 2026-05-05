# Durable Functions Middleware Sample

This sample shows how to use Durable Task middleware from a .NET isolated Durable Functions app that uses the standard `[Function]` syntax.

## What it demonstrates

- Registering orchestration middleware with `UseOrchestrationMiddleware<T>()`.
- Registering activity middleware with `UseActivityMiddleware<T>()`.
- Reading durable context such as function name, instance ID, input, and result.
- Using `GetFunctionContext()` from middleware to access the Azure Functions `FunctionContext`.
- Using replay-safe logging from orchestration middleware.

## Run locally

Start Azurite or another storage emulator, then run:

```powershell
cd samples\durable-middleware
func start
```

Start the orchestration:

```powershell
curl.exe -X POST http://localhost:7071/api/orchestrators/greeting
```

The Functions host logs show both orchestration and activity middleware running around the function-syntax Durable Functions methods.

Example log output:

```text
Starting orchestration GreetingOrchestration (<instance ID>) from function GreetingOrchestration with input Tokyo.
Starting activity SayHello for instance <instance ID> from function SayHello with input Tokyo.
Completed activity SayHello for instance <instance ID> with result Hello, Tokyo!.
Completed orchestration GreetingOrchestration (<instance ID>) with result Hello, Tokyo!.
```

## Determinism note

Orchestration middleware runs during orchestrator replay. Do not call non-deterministic APIs such as `DateTime.Now`, `Guid.NewGuid()`, `Random`, file I/O, network I/O, or non-durable async APIs from orchestration middleware. Use `context.IsReplaying` and `context.OrchestrationContext.CreateReplaySafeLogger` for replay-safe logging.
