## New Features
- Support specification of max entity operation batch size
- Added a boolean property `IncludeDeleted` to `EntityQuery` which controls whether to return deleted entities.
- Azure Durable Functions now supports identity-based connections. See [here](https://docs.microsoft.com/en-us/azure/azure-functions/functions-reference?tabs=blob#connecting-to-host-storage-with-an-identity-preview) for details on how to configure these connections ([#2014](https://github.com/Azure/azure-functions-durable-extension/pull/2014)) - contributed by [@wsugarman](https://github.com/wsugarman)
- `IConnectionStringResolver` has been deprecated in favor of `IConnectionInfoResolver`
  - Similarly, both `StandardConnectionStringProvider` and `WebJobsConnectionStringProvider` have been deprecated in favor of `StandardConnectionInfoProvider` and `WebJobsConnectionInfoProvider`

## Bug fixes
- Fixed handling of function timeouts inside entity and activity functions and added tests
- Skip constructor of AzureStorageDurabilityProvider if not used, to avoid spurious validation exceptions
- Fixed stuck orchestration issue caused when CallEntityAsync was the first action in an orchestration and the entity completed before the orchestrator completed its first history checkpoint. (fixed in DT.AzureStorage https://github.com/Azure/durabletask/pull/657)
- Fixed a Distributed Tracing bug where a StorageException would sometimes occur due to incorrect compression of the correlation field. (fixed in DT.AzureStorage https://github.com/Azure/durabletask/pull/649)

## Breaking Changes
- By default, `IDurableEntityClient.ListEntitiesAsync` no longer returns deleted entities.

## Dependency Updates
- Added [Microsoft.Extensions.Azure](https://www.nuget.org/packages/Microsoft.Extensions.Azure/1.1.1) v1.1.1 as a dependency for Azure Functions 2.0 and beyond
- Azure.Identity 1.1.1 -> 1.5.0 for Azure Functions 2.0 and beyond
- Microsoft.Azure.DurableTask.AzureStorage 1.9.4 -> 1.10.1
- Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers 0.4.1 -> 0.4.2
