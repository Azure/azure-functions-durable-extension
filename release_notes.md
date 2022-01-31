## New Features
- Support specification of max entity operation batch size

- Added a boolean property `IncludeDeleted` to `EntityQuery` which controls whether to return deleted entities.

## Bug fixes
- Fixed handling of function timeouts inside entity and activity functions and added tests
- Skip constructor of AzureStorageDurabilityProvider if not used, to avoid spurious validation exceptions

## Breaking Changes

- By default, `IDurableEntityClient.ListEntitiesAsync` no longer returns deleted entities.

## Dependency Updates
