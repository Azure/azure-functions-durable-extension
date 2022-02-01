## New Features
- Support specification of max entity operation batch size

- Added a boolean property `IncludeDeleted` to `EntityQuery` which controls whether to return deleted entities.
- Azure Durable Functions now supports identity-based connections. See [here](https://docs.microsoft.com/en-us/azure/azure-functions/functions-reference?tabs=blob#connecting-to-host-storage-with-an-identity-preview) for details on how to configure these connections ([#2014](https://github.com/Azure/azure-functions-durable-extension/pull/2014)) - contributed by [@wsugarman](https://github.com/wsugarman)
- `IConnectionStringResolver` has been deprecated in favor of `IConnectionInfoResolver`

## Bug fixes
- Fixed handling of function timeouts inside entity and activity functions and added tests
- Skip constructor of AzureStorageDurabilityProvider if not used, to avoid spurious validation exceptions

## Breaking Changes

- By default, `IDurableEntityClient.ListEntitiesAsync` no longer returns deleted entities.

## Dependency Updates

- Updated [Azure.Identity](https://www.nuget.org/packages/Azure.Identity/1.5.0) dependency to v1.5.0 for Azure Functions 2.0 and beyond
- Added [Microsoft.Extensions.Azure](https://www.nuget.org/packages/Microsoft.Extensions.Azure/1.1.1) v1.1.1 as a dependency for Azure Functions 2.0 and beyond
