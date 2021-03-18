## New Features
- Added support to select a storage backend provider when multiple are installed (#1702): Select which storage backend to use by setting the `type` field under `durableTask/storageProvider` in host.json. If this field isn't set, then the storage backend will default to using Azure Storage.
- Improved concurrency defaults for the App Service Consumption plan (https://github.com/Azure/azure-functions-durable-extension/pull/1706)

## Bug Fixes:
- Properly used update management API URLs after a successful slot swap on Functions V3 (#1716)
- Fix race condition when multiple apps start with local RPC endpoints on the same VM in parallel. (#1719)
- Fix CallHttpAsync() to throw an HttpRequestException instead of a serialization exception if the target endpoint doesn't exist (#1718)

## Breaking changes
- Fix CallHttpAsync() to throw an HttpRequestException instead of a serialization exception if the target endpoint doesn't exist (#1718). This is a breaking change if you were handling `HttpRequestException`s by catching `FunctionFailedException`s.