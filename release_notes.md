## New Features
- Azure Durable Functions now supports identity-based connections. See [here](https://docs.microsoft.com/en-us/azure/azure-functions/functions-reference?tabs=blob#connecting-to-host-storage-with-an-identity-preview) for details on how to configure these connections ([#2014](https://github.com/Azure/azure-functions-durable-extension/pull/2014)) - contributed by [@wsugarman](https://github.com/wsugarman)
- `IConnectionStringResolver` has been deprecated in favor of `IConnectionInfoResolver`
  - Similarly, both `StandardConnectionStringProvider` and `WebJobsConnectionStringProvider` have been deprecated in favor of `StandardConnectionInfoProvider` and `WebJobsConnectionInfoProvider`

- Initial support for .NET Isolated

## Bug fixes

## Breaking Changes

## Dependency Updates

- Added .NET 6 target
- Added DurableTask.Sidecar dependency (.NET 6 only), with transitive dependency on Grpc.AspNetCore.Server v2.38
- Updated minimum C# compiler version to 9.0
- Added [Microsoft.Extensions.Azure](https://www.nuget.org/packages/Microsoft.Extensions.Azure/1.1.1) v1.1.1 as a dependency for Azure Functions 2.0 and beyond
- Azure.Identity 1.1.1 -> 1.5.0 for Azure Functions 2.0 and beyond
- Microsoft.Azure.WebJobs 3.0.14 -> 3.0.31 for for Azure Functions 2.0 and beyond
