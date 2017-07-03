# Installing Durable Functions
Durable Functions is an extension of Azure Functions which uses a brand new binding extensibility model. This extensibility model is in the very early stages of development (see [this](https://github.com/Azure/azure-webjobs-sdk-script/issues/1419) GitHub issue for updates) and does not yet provide a decent setup experience. Because of this, the setup for Durable Functions currently involves 1) manually deploying a set of files to the function app and 2) adding an app setting which points to the directory containing these files. In the coming weeks, we hope to reduce this down to a simple NuGet package reference.

In the meantime, you will need to follow the below manual steps to use Durable Functions in your function app.

## Using the Durable Task Binding Extension in Visual Studio (Windows Only)
If you can't or don't want to use Visual Studio for development, you can do development directly in Azure using the Azure Management portal. In that case, skip these instructions and go straight to the Azure instructions further below.

Visual Studio currently provides the best experience for evaluating Durable Functions. Here is how to get started:

1. Install the [Visual Studio Tools for Azure Functions](https://blogs.msdn.microsoft.com/webdev/2017/05/10/azure-function-tools-for-visual-studio-2017/) if you haven't already.
2. Download the [DurableFunctionsBinding.zip](~/files/DurableFunctionsBinding.zip) file and unzip its contents into your C:\ directory (any location will work, but the samples currently assume it's in the C:\ directory). Once unzipped, you should have a `C:\BindingExtensions` directory that contains a single folder named `DurableTask`. The `DurableTask` directory will contain several DLLs.
3. Create a new Function App project (an existing one also works).
4. Add a new app setting called `AzureWebJobs_ExtensionsPath` in your `local.settings.json` file. Set it to `C:\\BindingExtensions` (the `\\` is to escape the backslash, since this is a JSON file).
5. In Visual Studio, select **Tools** --> **NuGet Package Manager** --> **Manage NuGet Packages for Solution..**.
6. In the upper-right hand corner, click the "Settings" gear icon.
7. Add a new package source with `https://www.myget.org/F/azure-appservice/api/v3/index.json` as the **Source**, click **Update**, and click **OK**. The Durable Task packages are published to our team's myget feed (and not NuGet.org) because they are still in active development. Note that this is the same location for nightly builds of Azure Functions and Azure WebJobs packages.
8. Add the following NuGet package reference to your .csproj file:

```xml
<PackageReference Include="Microsoft.Azure.WebJobs.Extensions.DurableTask" Version="0.1.0-alpha" />
```

This allows your project to download and reference the DurableTask extension which is required for Durable Functions.

This should be sufficient for local F5 development. If you would like to publish your solution to Azure, then you'll need to follow the Azure instructions below.

## Deploying the Durable Task Binding Extension in Azure
The below steps assume you have a function app up and running. If you do not, go ahead and create one by navigating to https://functions.azure.com/signin and create a new function app there. It only requires a few clicks.

Deploying the extension requires uploading some assemblies and creating an app setting.

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