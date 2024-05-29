# Release Notes

## Microsoft.Azure.Functions.Worker.Extensions.DurableTask 1.2.0

### New Features

- Add `suspendPostUri` and `resumePostUri` to the list of returned URIs in `CreateCheckStatusResponseAsync`. (https://github.com/Azure/azure-functions-durable-extension/pull/2785)
- Fix `NotSupportedException` when calling `PurgeAllInstancesAsync` and `PurgeInstanceAsync`

### Bug Fixes

### Breaking Changes

### Dependency Updates

## Microsoft.Azure.WebJobs.Extensions.DurableTask <version>

### New Features

### Bug Fixes

- Fix support for distributed tracing v2 in dotnet-isolated and Java (https://github.com/Azure/azure-functions-durable-extension/pull/2634)
- Update Microsoft.DurableTask.\* dependencies to v1.0.5

### Breaking Changes

### Dependency Updates
