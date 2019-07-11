## Durable Functions

Durable Functions (an extension of [Azure Functions](https://functions.azure.com) and [Azure WebJobs](https://docs.microsoft.com/en-us/azure/app-service/web-sites-create-web-jobs)) enables writing *long-running*, *stateful* function orchestrations in code in a serverless environment (PaaS options and self-hosting are also supported).

[![Build status](https://ci.appveyor.com/api/projects/status/rsoa2rrjxmd9h8i1?svg=true)](https://ci.appveyor.com/project/appsvc/azure-functions-durable-extension)

This extension enables a new type of function called the *orchestrator function* that allows you to do several new things that differentiates it from an ordinary, stateless function:
* They are stateful workflows **authored in code**. No JSON schemas or designers.
* They can *synchronously* and *asynchronously* **call other functions** and **save output to local variables**.
* They **automatically checkpoint** their progress whenever the function awaits so that local state is never lost if the process recycles or the VM reboots.

The Durable Functions extension currently ships as the [Microsoft.Azure.WebJobs.Extensions.DurableTask](https://www.nuget.org/packages/Microsoft.Azure.WebJobs.Extensions.DurableTask) NuGet package that can be referenced by an Azure Functions Visual Studio project.

## Getting Started
Are you new to Durable Functions? If so, this is the place to start.
* [Overview](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-overview)
* [Installation](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-install)
* [Azure WebJobs hosting]()

## Samples / Walkthroughs
Here are some samples you can study and/or reference. These will help you 1) learn how to write Durable Functions by example and 2) learn the various capabilities of Durable Functions.
* [Function Chaining - Hello sequence](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-sequence)
* [Fan-out/Fan-in - Cloud backup](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-cloud-backup)
* [Monitors - Weather watcher](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-monitor)
* [Human interaction & timeouts - Phone verification](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-phone-verification)
* [Unit testing - xUnit & Moq](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-unit-testing)
* [Self hosting - WebJob SDK integration](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-webjobs-sdk)
* [Lifecycle event notifications - Azure Event Grid](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-event-publishing)

Using one of the sample projects is a great way to get up and running quickly and is the recommended way to get started. See the sample / walkthrough links above for specific instructions.

Or if you prefer to start diving into the sample code, there are samples available for several development platforms:
* [C# for Visual Studio](./samples/precompiled), including [unit test samples](./samples/VSSample.Tests)
* [C# scripts](./samples/csx)
* [C# for Azure WebJobs](./samples/webjobssdk)
* [F#](./samples/fsharp)
* [JavaScript (Functions v2 only)](./samples/javascript)

## Topical Guides
Here you will find comprehensive documentation with examples on all of the feature areas. It's *highly* recommended that you read through all of these topics before coding.
* [Bindings](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-bindings)
* [Checkpointing & replay](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-checkpointing-and-replay)
* [Custom orchestration status](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-custom-orchestration-status)
* [Instance management](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-instance-management)
* [HTTP APIs](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-http-api)
* [Error handling & compensation](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-error-handling)
* [Diagnostics](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-diagnostics)
* [Durable timers](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-timers)
* [External events](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-external-events)
* [Eternal orchestrations](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-eternal-orchestrations)
* [Preview features](https://docs.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-preview)
* [Singleton orchestrations](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-singletons)
* [Sub-orchestrations](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-sub-orchestrations)
* [Task hubs](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-task-hubs)
* [Versioning](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-versioning)
* [Performance & scale](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-perf-and-scale)
* [Disaster recovery and geo-distribution](https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-disaster-recovery-geo-distribution)
 
## API Reference

### Configuration
* [Host.json reference](https://docs.microsoft.com/en-us/azure/azure-functions/functions-host-json#durabletask)
### .NET Attributes
* [OrchestrationTriggerAttribute](https://azure.github.io/azure-functions-durable-extension/api/Microsoft.Azure.WebJobs.OrchestrationTriggerAttribute.html)
* [OrchestrationClientAttribute](https://azure.github.io/azure-functions-durable-extension/api/Microsoft.Azure.WebJobs.OrchestrationClientAttribute.html)
* [ActivityTriggerAttribute](https://azure.github.io/azure-functions-durable-extension/api/Microsoft.Azure.WebJobs.ActivityTriggerAttribute.html)

### .NET APIs / Bindings
* [DurableOrchestrationContext](https://azure.github.io/azure-functions-durable-extension/api/Microsoft.Azure.WebJobs.DurableOrchestrationContext.html)
* [DurableOrchestrationClient](https://azure.github.io/azure-functions-durable-extension/api/Microsoft.Azure.WebJobs.DurableOrchestrationClient.html)
* [DurableActivityContext](https://azure.github.io/azure-functions-durable-extension/api/Microsoft.Azure.WebJobs.DurableActivityContext.html)

You can browse all of our public .NET APIs [here](https://azure.github.io/azure-functions-durable-extension/api/Microsoft.Azure.WebJobs.html). JavaScript API reference docs are coming soon!

## Contributing

We welcome outside contributions. If you are interested in contributing, please take a look at our [CONTRIBUTING](./CONTRIBUTING.md) guide.

## License

This project is under the benevolent umbrella of the [.NET Foundation](http://www.dotnetfoundation.org/) and is licensed under [the MIT License](https://github.com/Azure/azure-webjobs-sdk/blob/master/LICENSE.txt)

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
