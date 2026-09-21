# Release Notes

## Microsoft.Azure.Functions.Worker.Extensions.DurableTask

### New Features

- Added client operation correlation logging: Out-of-process workers (.NET isolated, Python, JavaScript, Java) now propagate `FunctionInvocationId` to the host when making client operations (start, terminate, suspend, resume, raise event, rewind, purge). This enables correlating worker-side function invocations with host-side orchestration events in extension logs. (#3317)
- Add `GetFunctionContext` extension method on `TaskOrchestrationContext` to retrieve the underlying `FunctionContext` in Azure Functions orchestrations.

### Bug Fixes

- Sanitized the isolated worker's 202/Location polling log while retaining its `Polling HTTP status at location: ` prefix, existing category, Information level, and replay suppression. It logs the resolved escaped endpoint without query, user information, or fragment; full-URL/query-value parser compatibility is not preserved. The worker scheduling log remains available with older hosts, while new host send diagnostics and polling counts require the corresponding host/worker features. Updating only the host does not sanitize an older worker's raw log. With both diagnostics enabled, a poll can produce a worker scheduling record and a host send-attempt record, increasing log volume. (#2074)
- Check if function invocation already has an executor before registering durable executor. (#3265)
- Improved .NET isolated activity input deserialization diagnostics for both function-style and class-based activities. Failures now identify the activity and target type and preserve the original serializer error as the inner failure. (#3531)

### Breaking Changes

- For .NET isolated function-style and class-based activities, input deserialization failures now use `System.InvalidOperationException` as the top-level `TaskFailureDetails.ErrorType`; the original serializer exception type is preserved in `InnerFailure`. (#3531)

### Dependency Updates

## Microsoft.Azure.WebJobs.Extensions.DurableTask

### New Features

- Added structured Durable HTTP send diagnostics with the endpoint, query parameter names (not values), orchestration instance, and polling attempt. Initial requests, polls, retries, and failed send attempts are covered without logging headers or bodies. (#2074)
- Allow overriding orchestration version when starting orchestrations via APIs in PowerShell, Python, and Node.js (https://github.com/Azure/azure-functions-durable-extension/pull/3213)
- Added `IServiceCollection.AddDurableTaskTelemetryInitializer` overloads for explicitly enriching distributed tracing V2 requests and dependencies with Application Insights telemetry initializers or service-provider factories. Host initializers and processors are not inherited automatically. (#1792)
- Added `ClientOperationReceived` trace event to `DurableFunctionsEvents` for correlating out-of-process worker invocations with orchestration events. The event includes `FunctionInvocationId`, `OperationType`, and `InstanceId` fields for cross-log correlation. (#3317)
- Existing extension-generated `FunctionScheduled` trace events now include a `TargetInstanceId` field for entity, sub-orchestration, and Durable Client scheduling paths when the target ID is known. Sub-orchestrations without an explicit ID report the target as not supplied because DTFx generates it downstream. This change does not add `FunctionScheduled` events to modern gRPC worker middleware paths, which do not currently emit that event. (#1496)

### Bug Fixes

- The `/makeprimary` HTTP API now returns HTTP 400 with an actionable error when app leases are disabled, instead of returning HTTP 500. (#3534)
- Fixed a poison loop where dispatching a disabled-but-still-deployed activity or entity function caused in-flight orchestrations to retry indefinitely (e.g. throwing `ArgumentNullException('executor')` on the activity dispatch path) instead of failing gracefully. Such registered-but-inactive functions are now treated as unavailable and fail deterministically. (#3471)
- Fixed the Event Grid `Terminated` lifecycle notification never being published when an orchestration is terminated. It is now raised from the orchestration dispatch middleware, which covers both the in-process/legacy out-of-proc path and the middleware-passthrough path. Previously no notification was sent at all on the former, and the latter incorrectly published a `Completed` notification. (#286)
- Fixed empty Application Insights operation names for Distributed Tracing V2 orchestration and activity telemetry when instance ID suffixes are disabled. (#3156)

### Breaking Changes

### Dependency Updates

- Remove LegacyLocalGrpcListener and the dependency on Grpc.Core (https://github.com/Azure/azure-functions-durable-extension/pull/3236)
