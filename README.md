## Durable Functions

**Durable Functions** is an extension of [Azure Functions](https://functions.azure.com) and [Azure WebJobs](https://docs.microsoft.com/en-us/azure/app-service/web-sites-create-web-jobs) that allows writing *long-running*, *stateful* function orchestrations in code in a serverless environment.

This extension enables a new type of function called the *orchestrator function* that allows you to do several new things that differentiates it from an ordinary, stateless function:
* They are stateful workflows **authored in code**. No JSON schemas or designers.
* They can *synchronously* and *asynchronously* **call other functions** and **save output to local variables**.
* They **automatically checkpoint** their progress whenever the function awaits so that local state is never lost if the process recycles or the VM reboots.

The Durable Functions extension currently ships as the [Microsoft.Azure.WebJobs.Extensions.DurableTask](https://www.nuget.org/packages/Microsoft.Azure.WebJobs.Extensions.DurableTask) NuGet package that can be referenced by an Azure Functions Visual Studio project. The project is currently in a public preview "beta" status.

## License

This project is under the benevolent umbrella of the [.NET Foundation](http://www.dotnetfoundation.org/) and is licensed under [the MIT License](https://github.com/Azure/azure-webjobs-sdk/blob/master/LICENSE.txt)

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Documentation
The Durable Functions documentation is currently hosted at docs.microsoft.com. You can find quick links to relevant topics below.

## Getting Started
Are you new to Durable Functions? If so, this is the place to start.
* [Overview](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-overview)
* [Installation](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-install)

### Samples / Walkthroughs
Here are some samples you can study and/or reference. These will help you 1) learn how to write Durable Functions by example and 2) learn the various capabilities of Durable Functions.
* [Function Chaining - Hello Sequence](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-sequence)
* [Fan-out/Fan-in - Cloud Backup](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-cloud-backup)
* [Human Interaction & Timeouts - Phone Verification](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-phone-verification)

Also, you can download all the samples as a single function app, which you can download from one of these two links:
* C# Scripts: [DFSampleApp.zip](../../raw/master/docfx/files/DFSampleApp.zip)
* Visual Studio Project: [VSDFSampleApp.zip](../../raw/master/docfx/files/VSDFSampleApp.zip)

Using one of the sample projects is a great way to get up and running quickly and is the recommended way to get started. See the sample / walkthrough links above for specific instructions.

### Topical Guides
Here you will find comprehensive documentation with examples on all of the feature areas. It's *highly* recommended that you read through all of these topics before coding.
* [Bindings](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-bindings)
* [Checkpointing & Replay](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-checkpointing-and-replay)
* [Instance Management](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-instance-management)
* [HTTP APIs](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-http-api)
* [Error Handling & Compensation](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-error-handling)
* [Diagnostics](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-diagnostics)
* [Durable Timers](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-timers)
* [External Events](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-external-events)
* [Eternal Orchestrations](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-eternal-orchestrations)
* [Singleton Orchestrations](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-singletons)
* [Sub-Orchestrations](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-sub-orchestrations)
* [Task Hubs](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-task-hubs)
* [Versioning](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-versioning)
* [Performance & Scale](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-perf-and-scale)
 
### .NET API Reference
.NET API reference for the attributes and bindings provided by Durable Functions.

#### Attributes
* [OrchestrationTriggerAttribute](https://azure.github.io/azure-functions-durable-extension/api/Microsoft.Azure.WebJobs.OrchestrationTriggerAttribute.html)
* [OrchestrationClientAttribute](https://azure.github.io/azure-functions-durable-extension/api/Microsoft.Azure.WebJobs.OrchestrationClientAttribute.html)
* [ActivityTriggerAttribute](https://azure.github.io/azure-functions-durable-extension/api/Microsoft.Azure.WebJobs.ActivityTriggerAttribute.html)

#### Bindings
* [DurableOrchestrationContext](https://azure.github.io/azure-functions-durable-extension/api/Microsoft.Azure.WebJobs.DurableOrchestrationContext.html)
* [DurableOrchestrationClient](https://azure.github.io/azure-functions-durable-extension/api/Microsoft.Azure.WebJobs.DurableOrchestrationClient.html)
* [DurableActivityContext](https://azure.github.io/azure-functions-durable-extension/api/Microsoft.Azure.WebJobs.DurableActivityContext.html)

You can browse all of our public APIs [here](https://azure.github.io/azure-functions-durable-extension/api/Microsoft.Azure.WebJobs.html).
