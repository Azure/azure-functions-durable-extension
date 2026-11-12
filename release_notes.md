# Release Notes

## Release package versions

| Package | Version |
|---|---|
| `Microsoft.Azure.WebJobs.Extensions.DurableTask` | `3.19.0` |
| `Microsoft.Azure.Functions.Worker.Extensions.DurableTask` | `1.22.0` |
| `Microsoft.Azure.Functions.Worker.Extensions.DurableTask.AzureBlobPayloads` | `1.1.0` |
| `Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale` | `1.1.0` |

These minor releases incorporate the dependency updates from #3597. FunctionsScale version metadata is prepared here, but the current official build/publish pipeline does not package or publish FunctionsScale; its release requires a separate approved distribution path.

## Microsoft.Azure.Functions.Worker.Extensions.DurableTask

### New Features

- Added `SetLargePayloadAutoPurgeAsync` on normally bound `DurableTaskClient` instances to explicitly enable or disable external payload auto-purge for a task hub. Requires host extension `3.17.0` or later and a compatible storage provider. (#3556)
- Added client operation correlation logging: Out-of-process workers (.NET isolated, Python, JavaScript, Java) now propagate `FunctionInvocationId` to the host when making client operations (start, terminate, suspend, resume, raise event, rewind, purge). This enables correlating worker-side function invocations with host-side orchestration events in extension logs. (#3317)
- Add `GetFunctionContext` extension method on `TaskOrchestrationContext` to retrieve the underlying `FunctionContext` in Azure Functions orchestrations.

### Bug Fixes

- Check if function invocation already has an executor before registering durable executor. (#3265)
- Improved .NET isolated activity input deserialization diagnostics for both function-style and class-based activities. Failures now identify the activity and target type and preserve the original serializer error as the inner failure. (#3531)

### Breaking Changes

- For .NET isolated function-style and class-based activities, input deserialization failures now use `System.InvalidOperationException` as the top-level `TaskFailureDetails.ErrorType`; the original serializer exception type is preserved in `InnerFailure`. (#3531)

### Dependency Updates

- Updated the required host extension to `3.19.0` in worker `1.22.0` to align the dependency-update release set. (#3597)
- Updated `Microsoft.DurableTask.Client.Grpc`, `Microsoft.DurableTask.Worker.Grpc`, and `Microsoft.DurableTask.Abstractions` to 1.28.0, including entity-scheduled orchestration tags, external payload auto-purge support, and the fix for external-event loss after canceled waits in the isolated worker.
- Updated `Microsoft.DurableTask.LargePayloadPurge.Abstractions` to 1.28.0 to align purge contracts with the SDK client models.
- Updated `Microsoft.DurableTask.Analyzers` to 1.28.0 and the test source generator reference to the stable `Microsoft.DurableTask.Generators` 1.0.0 release.

## Microsoft.Azure.Functions.Worker.Extensions.DurableTask.AzureBlobPayloads

### New Features

- Initial `1.0.0` release supplies the SDK-owned purge orchestration and three activities as ordinary .NET 8 and .NET 10 isolated Functions. Blob deletion uses the worker's configured `PayloadStore`; referencing the package does not enable auto-purge or validate the store at startup. See [configuration and limitations](docs/large-payload-purge-dotnet-isolated.md). (#3556)

### Dependency Updates

- Version `1.1.0` requires isolated worker extension `1.22.0` or later (which references host extension `3.19.0`) and `Microsoft.DurableTask.Extensions.AzureBlobPayloads` 1.28.0; auto-purge still requires explicit enablement. (#3597)

## Microsoft.Azure.WebJobs.Extensions.DurableTask

### New Features

- Added the default-off `DurabilityProvider.AllowsExternalActivityScheduling` policy in host extension `3.18.0` for providers that route activities to other workers. Opting in skips only the local activity registration check in classic/in-process and HTTP-protocol orchestration scheduling; activity name validation, orchestrator/entity validation, and local execution availability checks remain unchanged. No global setting is added, and gRPC action passthrough is unchanged. Azure Managed support requires a companion provider release. (#3594)
- Added provider-backed external payload auto-purge control and tombstone fetch/report RPCs for .NET isolated Functions. Blob deletion runs in the isolated worker, not the common Functions host. (#3556)
- Allow overriding orchestration version when starting orchestrations via APIs in PowerShell, Python, and Node.js (https://github.com/Azure/azure-functions-durable-extension/pull/3213)
- Added `IServiceCollection.AddDurableTaskTelemetryInitializer` overloads for explicitly enriching distributed tracing V2 requests and dependencies with Application Insights telemetry initializers or service-provider factories. Host initializers and processors are not inherited automatically. (#1792)
- Added `ClientOperationReceived` trace event to `DurableFunctionsEvents` for correlating out-of-process worker invocations with orchestration events. The event includes `FunctionInvocationId`, `OperationType`, and `InstanceId` fields for cross-log correlation. (#3317)
- Existing extension-generated `FunctionScheduled` trace events now include a `TargetInstanceId` field for entity, sub-orchestration, and Durable Client scheduling paths when the target ID is known. Sub-orchestrations without an explicit ID report the target as not supplied because DTFx generates it downstream. This change does not add `FunctionScheduled` events to modern gRPC worker middleware paths, which do not currently emit that event. (#1496)

### Bug Fixes

- Fixed startup failures in non-production deployment slots when the Durable extension is referenced but no Durable bindings are used. Non-default task hub names are still required when indexing Durable bindings or using Durable clients and workers. (#2352)
- Preserved orchestration tags when .NET isolated entities schedule new orchestrations, using the released Durable Task SDK 1.28.0 and Core 3.11.2. Empty tags remain absent, and scheduling takes a snapshot of caller tags. (https://github.com/microsoft/durabletask-dotnet/issues/807)
- Fixed missing `FailureDetails` in .NET isolated `GetAllInstancesAsync` results when `OrchestrationQuery.FetchInputsAndOutputs` is enabled. Query results now preserve available exception types, messages, stack traces, inner failures, and custom properties. (#2047)
- Excluded disabled activities from the function capabilities supplied to work-item filtering providers at worker startup. With DTS filtering enabled, unmatched activity work can wait for an enabled worker instead of being dispatched to a worker where the activity is disabled. Unfiltered dispatch behavior is unchanged.
- The `/makeprimary` HTTP API now returns HTTP 400 with an actionable error when app leases are disabled, instead of returning HTTP 500. (#3534)
- Fixed a poison loop where dispatching a disabled-but-still-deployed activity or entity function caused in-flight orchestrations to retry indefinitely (e.g. throwing `ArgumentNullException('executor')` on the activity dispatch path) instead of failing gracefully. Such registered-but-inactive functions are now treated as unavailable and fail deterministically. (#3471)
- Fixed the Event Grid `Terminated` lifecycle notification never being published when an orchestration is terminated. It is now raised from the orchestration dispatch middleware, which covers both the in-process/legacy out-of-proc path and the middleware-passthrough path. Previously no notification was sent at all on the former, and the latter incorrectly published a `Completed` notification. (#286)
- Fixed empty Application Insights operation names for Distributed Tracing V2 orchestration and activity telemetry when instance ID suffixes are disabled. (#3156)
- Fixed the local gRPC listener failing to start when the port it had probed for became unavailable between the probe and the actual bind. Kestrel now requests a dynamic port (`0`) so the OS assigns it as part of the bind itself, closing the race window; the bound address is still discovered through `IServerAddressesFeature`. Previously this surfaced as a `FunctionListenerException` wrapping a `SocketException` and left the Durable trigger listener unable to start.

### Breaking Changes

- Per-function Durable execution logs now use `Function.<functionName>.User` instead of `Host.Triggers.DurableTask`; infrastructure diagnostics remain under the host category. Update category-specific `host.json` logging filters and telemetry queries, alerts, or dashboards as needed. (#3562)

### Dependency Updates

- Remove LegacyLocalGrpcListener and the dependency on Grpc.Core (https://github.com/Azure/azure-functions-durable-extension/pull/3236)
- Reference `Microsoft.AspNetCore.WebUtilities` 2.3.14 for CVE-2026-118750 while retaining normal SDK package pruning. The Functions host already supplies a newer, patched version.
- Removed the production dependency on `Microsoft.Azure.WebJobs.Logging.ApplicationInsights` to reduce the extension's transitive dependencies while preserving Microsoft Entra authentication for distributed tracing.
- Updated the Azure Managed SDK and Functions extensions to 1.12.0, including the Functions scale extension and E2E apps.
- Updated `Grpc.Net.Client` to 2.84.0 to meet the Durable Task SDK 1.28.0 dependency requirement. `Microsoft.Bcl.AsyncInterfaces` remains at 10.0.9 for Azure Managed SDK compatibility.
- Updated `Microsoft.Azure.DurableTask.AzureStorage` to 2.11.2, `Microsoft.Azure.DurableTask.Core` to 3.11.2, and `Microsoft.Azure.DurableTask.ApplicationInsights` to 0.13.1.
- Updated Netherite and its Azure Functions extension to 3.1.2, SQL Server and its Azure Functions extensions to 1.8.1, and the test emulator to 2.6.0.
- Updated package-based host extension consumers to 3.18.0, retaining 2.13.7 for `netstandard2.0` projects.
- Updated `Microsoft.DurableTask.LargePayloadPurge.Abstractions` to 1.28.0 to align purge contracts with the SDK client models.

## Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale

### Dependency Updates

- Version `1.1.0` updates `Microsoft.Azure.DurableTask.AzureStorage` to 2.11.2, `Microsoft.DurableTask.AzureManagedBackend` to 1.12.0, `Microsoft.Azure.DurableTask.Netherite` to 3.1.2, and `Microsoft.DurableTask.SqlServer` to 1.8.1. (#3597)
