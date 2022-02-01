## New Features
- Support specification of max entity operation batch size

- Added a boolean property `IncludeDeleted` to `EntityQuery` which controls whether to return deleted entities.

## Bug fixes
- Fixed handling of function timeouts inside entity and activity functions and added tests
- Skip constructor of AzureStorageDurabilityProvider if not used, to avoid spurious validation exceptions
- Fixed stuck orchestration issue caused when CallEntityAsync was the first action in an orchestration and the entity completed before the orchestrator completed its first history checkpoint (fixed in DT.AzureStorage https://github.com/Azure/durabletask/pull/657)

## Breaking Changes

- By default, `IDurableEntityClient.ListEntitiesAsync` no longer returns deleted entities.

## Dependency Updates
Microsoft.Azure.DurableTask.AzureStorage 1.9.4 -> 1.10.1
Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers 0.4.1 -> 0.4.2