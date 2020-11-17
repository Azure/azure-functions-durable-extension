# ASP.NET Core API To Do List Sample

This sample is an ASP.NET Core app that calls a Durable Function in an existing function app. The web application is a list of To Do list tasks and each time a task is added, a SetReminder function is called that schedules a reminder text to be sent to your phone after 24 hours to remind you to complete the task. The SetReminder orchestrator function is a function in an existing function app. The way the web application and function app are able to communicate is by having the same storage account and task hub. 

## Setup

Finish setting up the corresponding [Twilio text reminder function](https://github.com/Azure/azure-functions-durable-extension/blob/dev/samples/precompiled/SMSReminder.cs) by creating a Twilio account (if you don't have one) and setting the app settings.

Create a storage account and use the same connection string for the SMS reminder function app and this web application. Also, specify the same task hubs for both applications.

## License

See [LICENSE](https://github.com/Azure-Samples/dotnet-core-api/blob/master/LICENSE.md).

## Contributing

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.