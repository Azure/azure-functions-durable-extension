## Summary

Adds a missing `ContinueAsNew` override to `FunctionsOrchestrationContext` so that the new `ContinueAsNewOptions` (with `NewVersion`) is forwarded to the inner context instead of being silently dropped by the base class default.

## Problem

`TaskOrchestrationContext` gained a new virtual overload in microsoft/durabletask-dotnet#682:

```csharp
public virtual void ContinueAsNew(ContinueAsNewOptions? options, object? newInput, bool preserveUnprocessedEvents)
```

`FunctionsOrchestrationContext` wraps an inner `TaskOrchestrationContext` and overrides all methods to delegate. However, it only overrides the 2-param `ContinueAsNew(object?, bool)`, so the new 3-param overload falls through to the base class default which ignores `ContinueAsNewOptions` entirely. This means `NewVersion` is silently lost when called from Azure Functions isolated worker.

## Fix

One-line override that delegates to the inner context:

```csharp
public override void ContinueAsNew(ContinueAsNewOptions? options, object? newInput, bool preserveUnprocessedEvents)
{
    this.EnsureLegalAccess();
    this.innerContext.ContinueAsNew(options, newInput, preserveUnprocessedEvents);
}
```

## Dependencies

- **Requires**: microsoft/durabletask-dotnet#682 to be merged and a new `Microsoft.DurableTask.Worker.Grpc` NuGet package to be published (introduces `ContinueAsNewOptions`).
- The package reference version in the csproj will need to be bumped to the version containing `ContinueAsNewOptions`.

## Testing

- Build will not succeed until the NuGet dependency is updated.
- E2E validated locally by running the AzureFunctionsApp sample with Azurite — confirmed that without this override, `context.Version` returns empty string after ContinueAsNew with `NewVersion = "v2"`, and with this override it correctly returns `"v2"`.
