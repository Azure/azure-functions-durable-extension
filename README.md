**Durable Functions** is an extension of [Azure Functions](https://functions.azure.com) and [Azure WebJobs](https://docs.microsoft.com/en-us/azure/app-service/web-sites-create-web-jobs) that allows writing *long-running*, *stateful* function orchestrations in code in a serverless environment.

A new type of function called the *orchestrator function* allows you to do several new things that differentiates it from an ordinary, stateless function:
* They are stateful workflows **authored in code**. No JSON schemas or designers.
* They can *synchronously* and *asynchronously* **call other functions** and **save output to local variables**.
* They **automatically checkpoint** their progress whenever the function awaits so that local state is never lost if the process recycles or the VM reboots.

Durable Functions currently ships as the [Microsoft.Azure.WebJobs.Extensions.DurableTask](https://www.nuget.org/packages/Microsoft.Azure.WebJobs.Extensions.DurableTask) NuGet package which can be referenced by an Azure Functions Visual Studio project. The project is currently in a public preview "beta" status.

## Documentation

See [the documentation](https://azure.github.io/azure-functions-durable-extension/) for in-depth information about Durable Functions, including samples, walkthroughs, and setup instructions.

## License

This project is under the benevolent umbrella of the [.NET Foundation](http://www.dotnetfoundation.org/) and is licensed under [the MIT License](https://github.com/Azure/azure-webjobs-sdk/blob/master/LICENSE.txt)

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.