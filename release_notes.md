## New Features
* New `HttpRetryOptions` class for passing retry options to `CallHttpAsync()` invocations to add & customize retry behavior
* Added `HttpRetryOptions` to `DurableHttpRequest`; when using the `CallHttpAsync` overload that takes the full object, set this property in order to affect retry logic.
* Log trace events whenever entities are created or deleted

## Bug fixes
* Updated DurableTask.AzureStorage dependency to v1.9.2, which includes the following fixes:
* Fix fetching of large inputs for pending orchestrations on Azure Storage
* Updated TableQuery filter condition string generation to resolve invalid character issues
* Fixed stuck orchestration with duplicate message warning issue
* Fixed null reference exceptions thrown in DurableClient

## Breaking Changes
* `IDurableOrchestrationContext`'s `CallHttpAsync(HttpMethod, Uri, string)` overload now has a `HttpRetryOptions` parameter
* `IDurableActivityContext` now has a Name property.
