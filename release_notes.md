## New Features
* Ability to pass `RetryOptions` object to `CallHttpAsync()` invocations to add & customize retry behavior
* This required a breaking change to `IDurableOrchestrationContext` adding an overload to `CallHttpAsync` which takes the parameter

## Bug fixes
* Updated DurableTask.AzureStorage dependency to v1.9.2, which includes the following fixes:
* * Fix fetching of large inputs for pending orchestrations on Azure Storage
* * Updated TableQuery filter condition string generation to resolve invalid character issues
* * Fixed stuck orchestration with duplicate message warning issue
* * Throw meaningful exceptions inside orchestrations when they try to call, signal, or lock a non-existing entity

## Breaking Changes
