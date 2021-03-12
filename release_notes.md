### NuGet Package 
  
https://www.nuget.org/packages/Microsoft.Azure.WebJobs.Extensions.DurableTask/2.4.2

### Improvements
###### Fixed a bug with http uri decoding that disallowed use of encoded special characters as parameter values. (https://github.com/Azure/azure-functions-durable-extension/pull/1726)
In conjunction with a PR in the durable task repo (https://github.com/Azure/durabletask/pull/481) in the previous release that added a DT-AzureStorage specific encoding system to circumvent the Azure Storage character restrictions, we have loosened restrictions on orchestration instance ids and entity keys. /, \\, #, and ?, and when using HTTP APIs their encoded versions, are now valid inputs.

###### Improved concurrency defaults for the App Service Consumption plan (https://github.com/Azure/azure-functions-durable-extension/pull/1706)

### New Features
###### Added support to select a storage backend provider when multiple are installed (#1702)
Select which storage backend to use by setting the `type` field under `durableTask/storageProvider` in host.json. If this field isn't set, then the storage backend will default to using Azure Storage.


