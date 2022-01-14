## New Features

- Added a boolean property `IncludeDeleted` to `EntityQuery` which controls whether to return deleted entities.

## Bug fixes
- Fixed handling of function timeouts inside entity and activity functions and added tests

## Breaking Changes

- By default, `IDurableEntityClient.ListEntitiesAsync` no longer returns deleted entities.

## Dependency Updates
