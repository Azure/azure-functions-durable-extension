## New Features
* Ability to pass `RetryOptions` object to `CallHttpAsync()` invocations to add & customize retry behavior
* Adding `FailedRequestRetryOptions` to `DurableHttpRequest`; when using the `CallHttpAsync` overload that takes the full object, set this property in order to affect retry logic.

## Bug fixes
* Updated DurableTask.AzureStorage dependency to v1.9.2, which includes the following fixes:
* * Fix fetching of large inputs for pending orchestrations on Azure Storage
* * Updated TableQuery filter condition string generation to resolve invalid character issues
* * Fixed stuck orchestration with duplicate message warning issue

## Breaking Changes
* `IDurableOrchestrationContext`'s `CallHttpAsync(HttpMethod, Uri, string)` overload now has a `SerializableRetryOptions` parameter which is identical to `RetryOptions` except without the `Handle` property.
