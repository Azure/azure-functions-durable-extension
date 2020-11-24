# Azure Function App with Durable Client Dependency Injection

This project contains an Azure Function App that calls a Durable Function through a Durable Client dependency injection.

## Local setup

Create a local.settings.json file and add two properties: a `Storage` property and a `TaskHub` property. Add the storage account connection string that you are using for the Durable Function. Having the same storage account allows the two function apps to communicate.

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

Specify the task hub name in the Azure Function HTTP Trigger (`CallHelloSequence`) where it says `<TaskHubName>`. The provided storage account connection string and task hub name for this application must match the connection string and task hub name used for the Function App that stores the Durable Function.

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