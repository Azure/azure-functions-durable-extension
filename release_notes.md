# Release Notes

### New Features

### Bug Fixes


#### Microsoft.Azure.WebJobs.Extensions.DurableTask

- Fix handling of in-flight orchestrations and activities during host shutdown.
    - Previously these were considered "failed", now they will be retried.
    - This only affected dotnet-isolated and java workers.
  
#### Microsoft.Azure.Functions.Worker.Extensions.DurableTask

- Will now use DI-configured `JsonSerializerOptions` during `.GetInput<T>`. [#2470](https://github.com/Azure/azure-functions-durable-extension/issues/2470)

### Breaking Changes

### Dependency Updates
