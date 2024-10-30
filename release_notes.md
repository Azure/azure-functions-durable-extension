# Release Notes

## Microsoft.Azure.Functions.Worker.Extensions.DurableTask <version>

### New Features

- Added an `IFunctionsWorkerApplicationBuilder.ConfigureDurableExtension()` extension method for cases where auto-registration does not work (no source gen running). (#2950)

### Bug Fixes

- Made durable extension for isolated worker configuration idempotent, allowing multiple calls safely. (#2950)

### Breaking Changes

### Dependency Updates

## Microsoft.Azure.WebJobs.Extensions.DurableTask <version>

### New Features

### Bug Fixes

### Breaking Changes

### Dependency Updates
