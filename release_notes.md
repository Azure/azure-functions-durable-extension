# Release Notes

## Microsoft.Azure.Functions.Worker.Extensions.DurableTask 1.1.1

### New Features

- Add `CreateCheckStatusResponseAsync` APIs. (https://github.com/Azure/azure-functions-durable-extension/pull/2722)

### Bug Fixes

- Fix issue with isolated entities: custom deserialization was not working because IServices was not passed along (https://github.com/Azure/azure-functions-durable-extension/pull/2686)
- Fix issue with `string` activity input having extra quotes (https://github.com/Azure/azure-functions-durable-extension/pull/2708)
- Fix issue with out-of-proc entity operation errors: success/failure details of individual operations in a batch was not processed correctly (https://github.com/Azure/azure-functions-durable-extension/pull/2752)
- Fix issues with .NET Isolated out-of-process error parsing (see https://github.com/Azure/azure-functions-durable-extension/issues/2711)

### Breaking Changes

### Dependency Updates

## Microsoft.Azure.WebJobs.Extensions.DurableTask <version>

### New Features

### Bug Fixes

- Fix execution context / log scope leak in token renewal (https://github.com/Azure/azure-functions-durable-extension/pull/2782)

### Breaking Changes

### Dependency Updates
