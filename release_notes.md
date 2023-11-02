# Release Notes

## Microsoft.Azure.Functions.Worker.Extensions.DurableTask <version>

### New Features

### Bug Fixes

### Breaking Changes

### Dependency Updates

## Microsoft.Azure.WebJobs.Extensions.DurableTask <version>

### New Features

### Bug Fixes

- Fix issue where json token input (not a json object) was unwrapped before sending to an out-of-proc worker. This could then lead to deserialization issues as the wrapping quotes were missing.

### Breaking Changes

### Dependency Updates
