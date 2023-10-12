# Release Notes

## Microsoft.Azure.Functions.Worker.Extensions.DurableTask v1.1.0-preview.1

### New Features

- Updates to take advantage of new core-entity support
- Support entities for Isolated

### Bug Fixes

- Address input issues when using .NET isolated (#2581)[https://github.com/Azure/azure-functions-durable-extension/issues/2581]
- No longer fail orchestrations which return before accessing the `TaskOrchestrationContext`.

### Breaking Changes

### Dependency Updates
