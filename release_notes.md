# Release Notes

## Microsoft.Azure.Functions.Worker.Extensions.DurableTask

### New Features

- Added client operation correlation logging: Out-of-process workers (.NET isolated, Python, JavaScript, Java) now propagate `FunctionInvocationId` to the host when making client operations (start, terminate, suspend, resume, raise event, rewind, purge). This enables correlating worker-side function invocations with host-side orchestration events in extension logs. (#3317)
- Add `GetFunctionContext` extension method on `TaskOrchestrationContext` to retrieve the underlying `FunctionContext` in Azure Functions orchestrations.

### Bug Fixes

- Check if function invocation already has an executor before registering durable executor. (#3265)

### Breaking Changes

### Dependency Updates

## Microsoft.Azure.WebJobs.Extensions.DurableTask

### New Features

- Allow overriding orchestration version when starting orchestrations via APIs in PowerShell, Python, and Node.js (https://github.com/Azure/azure-functions-durable-extension/pull/3213)
- Added `ClientOperationReceived` trace event to `DurableFunctionsEvents` for correlating out-of-process worker invocations with orchestration events. The event includes `FunctionInvocationId`, `OperationType`, and `InstanceId` fields for cross-log correlation. (#3317)
- Existing extension-generated `FunctionScheduled` trace events now include a `TargetInstanceId` field for entity, sub-orchestration, and Durable Client scheduling paths when the target ID is known. Sub-orchestrations without an explicit ID report the target as not supplied because DTFx generates it downstream. This change does not add `FunctionScheduled` events to modern gRPC worker middleware paths, which do not currently emit that event. (#1496)

### Bug Fixes

- Fixed a poison loop where dispatching a disabled-but-still-deployed activity or entity function caused in-flight orchestrations to retry indefinitely (e.g. throwing `ArgumentNullException('executor')` on the activity dispatch path) instead of failing gracefully. Such registered-but-inactive functions are now treated as unavailable and fail deterministically. (#3471)
- Fixed the Event Grid `Terminated` lifecycle notification never being published when an orchestration is terminated. It is now raised from the orchestration dispatch middleware, which covers both the in-process/legacy out-of-proc path and the middleware-passthrough path. Previously no notification was sent at all on the former, and the latter incorrectly published a `Completed` notification. (#286)
- Fixed empty Application Insights operation names for Distributed Tracing V2 orchestration and activity telemetry when instance ID suffixes are disabled. (#3156)

### Breaking Changes

### Dependency Updates

- Remove LegacyLocalGrpcListener and the dependency on Grpc.Core (https://github.com/Azure/azure-functions-durable-extension/pull/3236)
