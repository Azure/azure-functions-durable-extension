# Installing Durable Functions
Durable Functions is an extension of Azure Functions which uses the new binding extensibility model, which is currently alpha quality (see [this](https://github.com/Azure/azure-webjobs-sdk-script/issues/1419) GitHub issue for updates). Setting up the durable extension currently involves manually deploying a set of files to the function app and adding an app setting which points to the directory containing these files.

The below steps assume you have a function app up and running. If you do not, go ahead and create one by navigating to https://functions.azure.com/signin and create a new function app there. It only requires a few clicks.

## Deploying the Durable Task Binding Extension
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