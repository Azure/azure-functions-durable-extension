# External payload auto-purge for .NET isolated Functions

The optional `Microsoft.Azure.Functions.Worker.Extensions.DurableTask.AzureBlobPayloads` package supplies the Durable Task SDK's purge orchestration and activities as ordinary .NET isolated Functions. It targets .NET 8 and .NET 10.

## Requirements

Use:

- The compatible Durable Task SDK `1.27.0` and Durable Task Core `3.11.2` dependency set.
- Compatible Durable Functions host, isolated worker extension, and storage provider packages that support external payload auto-purge.
- The same external payload storage account and container that the provider uses for the target task hub.

Every worker that can receive purge work for the task hub must use compatible packages and a valid payload-store configuration.

## Configure the payload store

Configure the store when building the isolated worker:

```csharp
using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(worker =>
    {
        worker.ConfigureLargePayloadPurgeFunctions(options =>
        {
            options.ConnectionString =
                Environment.GetEnvironmentVariable("PayloadStorageConnection")
                ?? throw new InvalidOperationException(
                    "PayloadStorageConnection is required.");
            options.ContainerName =
                Environment.GetEnvironmentVariable("PayloadStorageContainer")
                ?? throw new InvalidOperationException(
                    "PayloadStorageContainer is required.");
        });
    })
    .Build();

await host.RunAsync();
```

The credentials belong to the worker process. They are not copied from the Functions host or sent through a blob-deletion RPC. The account and container must match the external payload store configured by the provider for this worker's task hub.

Loading the optional package, worker startup, bound-client creation, ordinary client operations, and enable/disable calls do not resolve or construct a `PayloadStore`. You can register a custom `PayloadStore` instead of calling `ConfigureLargePayloadPurgeFunctions`.

Enabling auto-purge can succeed without a working local store. The deletion activity still requires a valid `PayloadStore`: a missing registration or invalid configuration fails at runtime when the worker activates the deletion Function. Configure the store on every worker that can receive purge work.

The built-in store is a singleton. Custom stores retain their registered DI lifetime and are resolved in the deletion Function's invocation scope. DI-created instances remain owned by the host or their scope. If you register an existing store instance, you remain responsible for disposing it.

## Enable or disable a task hub

Auto-purge is disabled until you explicitly enable it through a normally bound `DurableTaskClient`:

```csharp
await client.SetLargePayloadAutoPurgeAsync(
    true,
    cancellationToken: cancellationToken);

await client.SetLargePayloadAutoPurgeAsync(
    false,
    cancellationToken: cancellationToken);
```

The bound client's task hub and connection determine which hub is changed. Enable each task hub separately.

An enable call is not transactional; cancellation or failure does not roll back changes that already succeeded.

Enable auto-purge before retiring instances whose external payloads should be tracked. Enabling later does not backfill payloads retired while the setting was disabled.

Disabling auto-purge prevents new eligible retirements from being tracked. It does not terminate an already-running purge orchestration or cancel work that was already fetched.

Referencing the optional package, configuring the store, or restarting the host does not enable auto-purge.

## Reserved Functions and versioning

The package supplies these reserved Functions:

- `BlobPurgeJobOrchestrator`
- `GetLargePayloadTombstonesActivity`
- `DeleteExternalBlobActivity`
- `ReportLargePayloadPurgeResultsActivity`

Do not redeclare these names in your application.

The purge orchestration is SDK-owned and unversioned. This exemption applies only to the exact reserved orchestration. Business orchestration version policies and nonempty execution versions remain unchanged.

## Limitations

- This integration supports .NET isolated Functions only.
- It does not add an HTTP management API or automatic enablement.
- The common Functions host does not delete blobs; deletion runs in the isolated worker with the configured store.
- Fetch and report operations use the task hub of the executing worker.
- Rolling back to incompatible host, worker, Core, SDK, or provider packages can break active purge executions.
- Enabling auto-purge does not guarantee storage availability when purge activities run.
