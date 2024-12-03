# Release Notes

## Microsoft.Azure.Functions.Worker.Extensions.DurableTask (version)

### New Features

- Fail fast if extendedSessionsEnabled set to 'true' for the worker type that doesn't support extended sessions (https://github.com/Azure/azure-functions-durable-extension/pull/2732).
- Added an `IFunctionsWorkerApplicationBuilder.ConfigureDurableExtension()` extension method for cases where auto-registration does not work (no source gen running). (#2950)

### Bug Fixes

- Fix custom connection name not working when using IDurableClientFactory.CreateClient() - contributed by [@hctan](https://github.com/hctan)
- Made durable extension for isolated worker configuration idempotent, allowing multiple calls safely. (#2950)

### Breaking Changes

### Dependency Updates

## Microsoft.Azure.WebJobs.Extensions.DurableTask 2.13.7

### New Features

- Update MaxQueuePollingInterval default for Flex Consumption apps #2953

### Bug Fixes

### Breaking Changes

### Dependency Updates

- Microsoft.DurableTask.Grpc to 1.3.0
