### NuGet Package 
  
https://www.nuget.org/packages/Microsoft.Azure.WebJobs.Extensions.DurableTask/2.4.2

### Improvements

##### Fixed a bug with http uri decoding that disallowed use of encoded special characters as parameter values. (https://github.com/Azure/azure-functions-durable-extension/pull/1726)
In conjunction with a PR in the durable task repo (https://github.com/Azure/durabletask/pull/481) in the previous release that added a DT-AzureStorage specific encoding system to circumvent the Azure Storage character restrictions, we have loosened restrictions on orchestration instance ids and entity keys. /, \\, #, and ?, and when using HTTP APIs their encoded versions, are now valid inputs.

