# Release Notes

### New Features

### Bug Fixes

- Fix handling of in-flight orchestrations and activities during host shutdown.
    - Previously these were considered "failed", now they will be retried.
    - This only affected dotnet-isolated and java workers.

### Breaking Changes

### Dependency Updates
