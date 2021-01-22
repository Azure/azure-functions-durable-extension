# Azure Function App with Durable Client Dependency Injection

This project contains an Azure Function App that calls a Durable Function through a Durable Client dependency injection.

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