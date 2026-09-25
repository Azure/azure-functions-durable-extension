# External payload auto-purge in .NET isolated Functions

This integration runs the Durable Task SDK's purge orchestration and activities as ordinary .NET isolated Functions. The Functions host indexes them and sends their invocations to the language worker. It does not execute native provider tasks, create a second task worker, or run an automatic enablement monitor.

## Register the SDK functions

Reference the optional `Microsoft.Azure.Functions.Worker.Extensions.DurableTask.AzureBlobPayloads` package in a .NET 8 or .NET 10 isolated app. The base Durable worker extension alone does not add any purge functions or depend on the SDK Blob payload implementation. Configure the shared store before the worker starts, using the same payload account and container configured for the task hub's storage provider:

```csharp
var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(worker =>
    {
        worker.ConfigureLargePayloadPurgeFunctions(options =>
        {
            options.ConnectionString = configuration["PayloadStorageConnection"];
            options.ContainerName = "durabletask-payloads";
        });
    })
    .Build();
```

The shared SDK store also supports an account URI and token credential. Configure credentials in the worker process; they are not copied out of the host or transported through a blob-deletion RPC.

The optional package automatically validates that a `PayloadStore` is registered and can be constructed before the isolated worker's hosted service starts accepting work. Omitting the configuration helper does not skip validation: a missing store or invalid configuration fails worker startup with a configuration error. An existing custom `PayloadStore` registration is also supported without calling the helper. Validation resolves the store in its own DI scope, supporting scoped stores without disposing container-owned singletons.

This is a configuration check, not a storage network probe or a guarantee of credentials, permissions, or later availability. It does not enable purge or write backend settings. Base-only client applications and remote applications that call the enable/disable API do not need a local store.

The SDK supplies these four Functions; do not copy their implementations into the application or declare other Functions with these names:

- `BlobPurgeJobOrchestrator`
- `GetLargePayloadTombstonesActivity`
- `DeleteExternalBlobActivity`
- `ReportLargePayloadPurgeResultsActivity`

The standard Worker SDK discovers the optional assembly's `[Function]` methods and generates their normal metadata and invocation entries, together with automatic startup validation. The function entry points remain in the optional Functions assembly and delegate to the shared SDK implementations. Fetch/report use ordinary `DurableClient` input bindings to the executing worker's task hub and do not require a store for their own activation. Only the deletion activity requires the store; it delegates to the shared SDK's payload ownership, ETag, retry, and quarantine implementation.

## Explicitly enable or disable

Use a normally bound `DurableTaskClient`:

```csharp
await client.SetLargePayloadAutoPurgeAsync(true, batchSize: 100, cancellationToken);
await client.SetLargePayloadAutoPurgeAsync(false, cancellationToken: cancellationToken);
```

The SDK enable operation writes the authenticated target hub's setting, ensures its fixed-ID orchestration has started without replacing live executions, verifies its identity, and raises its batch-size event. These operations are not a transaction; cancellation or failure does not undo earlier successful steps.

Disable only writes `false`. It does not create or terminate an orchestration; already-fetched work may finish. The backend setting also gates tombstone creation at retirement: enable before purging instances whose external payloads should be tracked. Payloads retired while disabled are not backfilled when enabling later. Including the optional package, configuring its store, or restarting the host does not enable cleanup. There is no primary-host election, `host.json` desired-state setting, or automatic enablement callback.

Explicit `DurableClient` task-hub and connection overrides apply to the target of the enable/disable API. Another target hub needs a compatible worker with these functions registered and its own correctly configured payload store. Fetch/report activities always use the hub of their executing Functions worker, not a captured global client from an earlier invocation.

## Versioning and compatibility

The SDK purge orchestration is explicitly unversioned, even when the application uses a business default version. Its SDK-owned orchestration metadata carries a reserved marker. The host accepts that marker only for the exact canonical orchestration name in .NET isolated and passes that indexed name to the Core unversioned-version exemption before worker startup. This changes only version matching, not function resolution or execution. Customer version policies and nonempty execution versions remain unchanged.

Every worker eligible to receive these functions must use compatible extension, Core, and provider versions. Older workers can reject or fail the reserved orchestration. Do not roll back while removing support needed by existing purge executions.

The underlying orchestration service client must implement `DurableTask.LargePayloadPurge.IOrchestrationServiceLargePayloadPurgeClient` from the separate `Microsoft.Azure.DurableTask.LargePayloadPurge.Abstractions` package, maintained in the [Durable Task .NET SDK repository](https://github.com/microsoft/durabletask-dotnet). That package defines two interfaces: the service capability and `Microsoft.DurableTask.AzureBlobPayloads.ILargePayloadPurgeClient`, the worker client's transport facade. Both reuse the existing `Microsoft.DurableTask.Client` tombstone/result records and disposition enum. It depends on the SDK Client package and its transitive dependencies, but not on the Blob implementation, Azure Storage, worker, or gRPC implementation packages; it is not a BCL-only contract. Core supplies only the generic version exemption and does not depend on this package or define these purge types.

The base `DurabilityProvider` forwards the calls directly to its inner service client, so a Functions provider needs no additional purge interface, mapping, or client factory. The local gRPC service forwards only the existing set/fetch/report operations through the provider selected by the normal binding's hub and connection headers, preserving caller-supplied deadlines and cancellation. Unsupported inner clients report `Unimplemented`; the common host does not implement storage deletion. Applications still opt into only the optional Functions package; the service contract is a transitive host/provider dependency.

This first integration is limited to .NET isolated Functions. It adds no HTTP management API or support for other language workers.

## Development dependency gate

This change requires the SDK-owned purge abstractions package and public purge integration types ([SDK integration change](https://github.com/microsoft/durabletask-dotnet/pull/805)), plus Core's exact-unversioned exemption ([Core versioning change](https://github.com/Azure/durabletask/pull/1408)). The abstractions package's `0.1.0` reference matches its planned first version, not an already published release. Until the prerequisites are published and actual references are aligned, validation uses explicit local package overrides. An ordinary build against the older committed dependencies is not evidence that the integration is ready to release. Hosts and providers built against an earlier unreleased Core-owned or Core-signed contract must be rebuilt against the SDK-owned package and canonical SDK models. Worker consumers must also rebuild after the client facade's move from the Blob implementation into the contracts assembly; matching package/type names alone do not guarantee compatible assembly identities. No duplicate compatibility types or type forwarders are supplied.

The local test app is `test/e2e/Apps/LargePayloadPurgeDotNetIsolated`. By default it references the optional project; setting `PurgeFunctionsPackageVersion` selects a package-only consumer. `DurableTaskHostPackageVersion` overrides only the common host package in the Worker SDK's generated extensions project. Neither property changes production package versions.

Its HTTP endpoints are test-only controls for explicit enable/disable, binding overrides, large payload creation, query, and ordinary instance purge. Its invocation middleware records the worker process ID and entry point for verifying actual language-worker execution. Use only an isolated local backend and payload account.

`test/e2e/Validate-LargePayloadPurge.ps1` accepts a running loopback host URL and an evidence directory. Run its preparation phase to enable the hub and create a payload, then obtain that instance's actual v2 references from the test backend before the purge phase:

```powershell
# Supply the local feed configuration and package versions built from the coordinated changes.
dotnet build .\test\e2e\Apps\LargePayloadPurgeDotNetIsolated\app.csproj `
    "-p:PurgeFunctionsPackageVersion=$OptionalPackageVersion" `
    "-p:DurableTaskHostPackageVersion=$HostPackageVersion" `
    "-p:DirectoryPackagesPropsPath=$PackageOverrideProps" `
    "-p:RestoreConfigFile=$LocalNuGetConfig"

.\test\e2e\Validate-LargePayloadPurge.ps1 -BaseUri $LoopbackHostUri -ArtifactsDirectory $PreparationEvidence
# $InstanceId and $ReferencedBlobNames come from the preparation result and a read-only backend observation.
.\test\e2e\Validate-LargePayloadPurge.ps1 -BaseUri $LoopbackHostUri -ArtifactsDirectory $PurgeEvidence `
    -InstanceId $InstanceId -ReferencedBlobNames $ReferencedBlobNames
```

Configure `PurgeTestHub`, `PurgePayloadConnection`, `PurgePayloadContainer`, and the backend's normal connection settings on the test host process. The override case also needs `PurgeOverrideHub` and a `PurgeOverrideConnection` app setting. The test's 384 KiB payload must exceed the configured externalization threshold; use 256 KiB for that threshold. The driver does not provision resources or start/stop the host. Its physical deletion assertion covers only the observed referenced blob names, not every blob in the container; capture the backend ledger separately to verify retirement and report acknowledgement.
