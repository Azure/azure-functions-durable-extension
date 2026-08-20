# Azure Function App with Durable Client Dependency Injection

This project contains an Azure Function App that calls a Durable Function through a Durable Client dependency injection.

## Samples in this project

### ClientFunction.cs
Demonstrates how to use IDurableClientFactory with dependency injection to create and use durable clients.

### OptionsFormatterSample.cs
Demonstrates how to use `IOptionsFormatter` with `DurableTaskOptions` to retrieve formatted configuration for diagnostics and troubleshooting. This sample shows:
- How to inject `IOptions<DurableTaskOptions>` into a function
- How to cast to `IOptionsFormatter` to access the `Format()` method
- How to log formatted configuration for diagnostics
- Example HTTP endpoints that return formatted configuration

The `IOptionsFormatter` interface is automatically used by Azure Functions infrastructure to format configuration options as JSON for diagnostics, logging, and monitoring purposes.

## Local setup

In the local.settings.json file, add values for "Storage" and "TaskHub". Add the storage account connection string and task hub name that you are using for the Durable Function. This Function App and the Durable Function communicate through the storage account and task hub.

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet",
    "Storage": "<storage account connection string>",
    "TaskHub": "<task hub name>"
  }
}
```

This sample calls the orchestrator function, `E1_HelloSequence` found in the [precompiled samples folder](https://github.com/Azure/azure-functions-durable-extension/blob/dev/samples/precompiled/HelloSequence.cs). Make sure that function is running at the same time.

## Running the sample locally
Send an HTTP request to the Azure Function. This will trigger the function, which will call the E1_HelloSequence orchestrator to start.

The JSON response will look something like the following (formatted for readability):

```JSON
{
  "name": "E1_HelloSequence",
  "instanceId": "36a1d82fb9064275b1df810b5962d4e0",
  "runtimeStatus": "Completed",
  "input": null,
  "customStatus": null,
  "output": [
    "Hello Tokyo!",
    "Hello Seattle!",
    "Hello London!"
  ],
  "createdTime": "2019-12-18T19:02:42Z",
  "lastUpdatedTime": "2019-12-18T19:02:42Z"
}
```

## Testing the OptionsFormatterSample

### GetDurableTaskOptions endpoint
Send a GET request to retrieve the formatted DurableTaskOptions configuration:

```bash
GET http://localhost:7071/api/GetDurableTaskOptions
```

The response will contain the formatted JSON configuration, for example:

```json
{
  "HubName": "TestHubName",
  "DefaultVersion": null,
  "VersionMatchStrategy": "CurrentOrOlder",
  "VersionFailureStrategy": "Reject",
  "MaxConcurrentActivityFunctions": null,
  "MaxConcurrentOrchestratorFunctions": null,
  "MaxConcurrentEntityFunctions": null,
  "ExtendedSessionsEnabled": false,
  "ExtendedSessionIdleTimeoutInSeconds": 30,
  "MaxOrchestrationActions": 100000,
  "UseAppLease": true,
  "HttpSettings": { ... },
  "Tracing": { ... },
  "Notifications": { ... },
  "AppLeaseOptions": { ... }
}
```

Note: `StorageProvider` is intentionally excluded from the formatted output to prevent exposing connection strings and other sensitive configuration data.

### LogDurableTaskOptionsOnStartup endpoint
This endpoint demonstrates logging specific configuration values:

```bash
GET http://localhost:7071/api/LogDurableTaskOptionsOnStartup
```

Check the function logs to see the logged configuration values.