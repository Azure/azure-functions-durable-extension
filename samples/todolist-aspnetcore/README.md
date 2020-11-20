# ASP.NET Core API To Do List Sample

This sample is based off of the [To Do List sample](https://github.com/Azure-Samples/dotnet-core-api) in the Azure-Samples repo. The original sample is an ASP.NET Core web application that keeps track of a To Do List where you are able to add, edit, delete and check off tasks. This sample adds to that functionality by calling a [SetReminder Durable Function](https://github.com/Azure/azure-functions-durable-extension/blob/dev/samples/precompiled/SMSReminder.cs) every time a task is added to the list. The SetReminder function schedules a reminder text to be sent to your phone after 24 hours to remind you to complete the task. 

The provided storage account connection string and task hub name for this application must match the connection string and task hub name used for the function app.

## Setup

Finish setting up the corresponding [Twilio text reminder function](https://github.com/Azure/azure-functions-durable-extension/blob/dev/samples/precompiled/SMSReminder.cs) by creating a Twilio account (if you don't have one) and setting the app settings.

Create a storage account and use the same connection string for the SMS reminder function app and this web application. Also, specify the same task hubs for both applications.