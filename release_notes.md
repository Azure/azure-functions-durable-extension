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

### Bug Fixes

### Breaking Changes

### Dependency Updates

- Remove LegacyLocalGrpcListener and the dependency on Grpc.Core (https://github.com/Azure/azure-functions-durable-extension/pull/3236)
