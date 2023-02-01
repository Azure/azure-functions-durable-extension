# Release Notes

## Microsoft.Azure.Functions.Worker.Extensions.DurableTask

1.0.0 GA release of Durable Functions for .NET isolated worker. This release includes support for running Orchestrations and Activities in the isolated worker. Entities are not supported yet.

## Microsoft.Azure.WebJobs.Extensions.DurableTask

- Added support for per-trigger named task hub and connections for gRPC based OOP workers.

### Bug fixes

- Fix deserialization of class-based entities to use custom serializer settings (resolves https://github.com/Azure/azure-functions-durable-extension/issues/2361)
