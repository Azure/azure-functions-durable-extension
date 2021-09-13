## New Features
* Ability to pass `RetryOptions` object to `CallHttpAsync()` invocations to add & customize retry behavior
* Adding `FailedRequestRetryOptions` to `DurableHttpRequest`; when using the `CallHttpAsync` overload that takes the full object, set this property in order to affect retry logic.

## Bug fixes

## Breaking Changes
* `IDurableOrchestrationContext`'s `CallHttpAsync(HttpMethod, Uri, string)` overload now has a `SerializableRetryOptions` parameter which is identical to `RetryOptions` except without the `Handle` property.
