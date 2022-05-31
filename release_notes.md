## Bug Fixes
- Correctly Serialize HostStoppingEvent in ActivityShim (https://github.com/Azure/azure-functions-durable-extension/pull/2178)
- Fix NotImplementedException for management API calls from Java client (https://github.com/Azure/azure-functions-durable-extension/pull/2193)

## Enhancements
- add optional 'instanceIdPrefix' query parameter to the HTTP API for instance queries

## Dependencies
- DurableTask.Core --> v2.10.*
- DurableTask.AzureStorage --> v1.12.*