## Bug Fixes
- Correctly Serialize HostStoppingEvent in ActivityShim (https://github.com/Azure/azure-functions-durable-extension/pull/2178)
- Fix NotImplementedException for management API calls from Java client (https://github.com/Azure/azure-functions-durable-extension/pull/2193)
- Handle OOM and other exceptions in entity shim by aborting the session (https://github.com/Azure/azure-functions-durable-extension/pull/2234)

## Enhancements
- add optional 'instanceIdPrefix' query parameter to the HTTP API for instance queries

## Dependencies
- DurableTask.Core --> v2.10.*
- DurableTask.AzureStorage --> v1.12.*
- DurableTask.Analyzers --> 0.5.0