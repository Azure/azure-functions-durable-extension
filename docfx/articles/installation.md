# Installing Durable Functions
Durable Functions is an extension of Azure Functions which uses a brand new binding extensibility model. This extensibility model is still in the early stages of development and not yet finalized. If you're interested, you can track the status of this extensibility [in GitHub](https://github.com/Azure/azure-webjobs-sdk-script/issues/1419).

When using Visual Studio to author functions using this Durable Task extension, you can simply add a NuGet reference to the `Microsoft.Azure.WebJobs.Extensions.DurableTask`. For Azure Portal development, manual setup is required (details below).

## Using the Durable Task Binding Extension in Visual Studio (Recommended - Windows Only)
Visual Studio currently provides the best experience for evaluating Durable Functions. Here is how to get started:

1. Install the [latest version of Visual Studio](https://www.visualstudio.com/downloads/) (VS 2017 15.3 or greater) if you haven't already and include the Azure tools in your setup options.
2. Create a new Function App project. Even better, start with the [Visual Studio Sample App (.zip)](~/files/VSDFSampleApp.zip).
3. In Visual Studio, select **Tools** --> **NuGet Package Manager** --> **Manage NuGet Packages for Solution..**.
4. In the upper-right hand corner, click the "Settings" gear icon.
5. Add a new package source with `https://www.myget.org/F/azure-appservice/api/v3/index.json` as the **Source**, click **Update**, and click **OK**. The Durable Task packages are published to our team's myget feed (and not NuGet.org) because they are still in active development. Note that this is the same location for nightly builds of Azure Functions and Azure WebJobs packages.
6. Add the following NuGet package reference to your .csproj file (NOTE: the sample app already has this):

```xml
<PackageReference Include="Microsoft.Azure.WebJobs.Extensions.DurableTask" Version="0.2.2-alpha" />
```

This allows your project to download and reference the **DurableTask** extension which is required for Durable Functions. Your functions can be run locally and can also be published to and run in Azure.

## Using the Durable Task Binding Extension for Portal Development
A word of caution: the Azure Functions extensibility model currently has only primitive support for binding extensions, so the setup is going to be more involved than it is for Visual Studio projects.

The below steps assume you have a function app up and running. If you do not, go ahead and create one by navigating to https://functions.azure.com/signin and create a new function app there. It only requires a few clicks.

When NOT using Visual Studio for publishing function apps, the the Durable Task extension needs to be uploaded and configured manually. Here are the required steps:

1. Open your function app in the [Azure Functions portal](https://functions.azure.com/signin).
2. Navigate to the Kudu console (Platform Features --> Advanced Tools (Kudu))
3. Click **Debug Console** and select **CMD**
4. Drag/drop the [DurableFunctionsBinding.zip](~/files/DurableFunctionsBinding.zip) file onto the right-side of the directory list in the browser window to unzip the contents into your function app.

> [!NOTE]
> Chrome is the recommended browser for drag-dropping zip files. If you can't or don't want to use Chrome or drag/drop isn't working, you can alternatively upload the zip file using FTP and use the `unzip` command in the Kudu console window to unzip the contents in the remote file system.

At this point you should have a `D:\home\BindingExtensions` directory which contains a single folder named `DurableTask`. The `DurableTask` directory will contain several DLLs.

5. Back in the Functions portal, click **Application settings** (under *General Settings*)
6. Add a new app setting named `AzureWebJobs_ExtensionsPath` and set the value to `D:\home\BindingExtensions`.
7. Stop and start your function app to load the extension.

> [!WARNING]
> Do NOT point your app setting to the **DurableTask** directory or else the runtime will not find your extension.

At this point, your binding extension should be installed and you can start using it in your function code!