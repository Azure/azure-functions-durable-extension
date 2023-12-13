# Release Notes

## Microsoft.Azure.Functions.Worker.Extensions.DurableTask <version>

### New Features

### Bug Fixes

- Fix issue with isolated entities: custom deserialization was not working because IServices was not passed along

### Breaking Changes

### Dependency Updates

## Microsoft.Azure.WebJobs.Extensions.DurableTask <version>

### New Features

### Bug Fixes

- Fix issue where json token input (not a json object) was unwrapped before sending to an out-of-proc worker. This could then lead to deserialization issues as the wrapping quotes were missing. (Applies to dotnet-isolated and java only)
- Fix failed orchestration/entities not showing up as function invocation failures. (Applies to dotnet-isolated and java only)

### Breaking Changes

### Dependency Updates
