# Durable Functions

[![Build status](https://ci.appveyor.com/api/projects/status/rsoa2rrjxmd9h8i1?svg=true)](https://ci.appveyor.com/project/appsvc/azure-functions-durable-extension)

Durable Functions is an extension that helps developers build reliable, stateful apps on the [Azure Functions](https://functions.azure.com) platform.

This extension adds three new types functions to the Azure Functions family:

* **[Orchestrator functions](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-orchestrations)**: Long-running, reliable workflow functions written in code that schedule and coordinate other functions.
* **[Activity functions](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-types-features-overview#activity-functions)**: Stateless functions that are the basic unit of work in a durable function orchestration.
* **[Entity functions](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-entities)**: Stateful [actor](https://en.wikipedia.org/wiki/Actor_model)-like functions that have identities and store state explicitly.

Durable Functions can run anywhere that Azure Functions can run, including in the Azure Functions "Serverless" [Consumption plan](https://docs.microsoft.com/azure/azure-functions/functions-scale#consumption-plan), the [Elastic Premium plan](https://docs.microsoft.com/azure/azure-functions/functions-scale#premium-plan), on [Kubernetes](https://docs.microsoft.com/azure/azure-functions/functions-kubernetes-keda), or even locally for development using [Visual Studio](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-create-first-csharp) or [Visual Studio Code](https://docs.microsoft.com/azure/azure-functions/functions-develop-vs-code).

📑 **[Official documentation](https://docs.microsoft.com/en-us/azure/azure-functions/durable/)** 📑

For a more detailed overview, including examples of what you can do with Durable Functions, see our [What is Durable Functions?](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-overview) article.

## Language support

Durable Functions supports a subset of languages supported by Azure Functions:

| Language   | Status | Repo |
|------------|------------------|-|
| C#         | Generally available - [get started](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-create-first-csharp) | [Azure/azure-functions-durable-extension](https://github.com/Azure/azure-functions-durable-extension) |
| JavaScript | Generally available - [get started](https://docs.microsoft.com/azure/azure-functions/durable/quickstart-js-vscode) | [Azure/azure-functions-durable-js](https://github.com/Azure/azure-functions-durable-js) |
| Python     | In development - [give feedback](https://github.com/Azure/azure-functions-python-worker/issues/227#issuecomment-542308187) | |
| PowerShell | In development - [give feedback](https://github.com/Azure/azure-functions-powershell-worker/issues/77#issuecomment-528997103) | |
| Java       | Under consideration - [give feedback](https://github.com/Azure/azure-functions-java-worker/issues/213) | |

Each language has its own language-specific SDK and programming model. Regardless of which language you use, the extension in this repo must be installed to enable the Durable Functions triggers.

## Installation

The Durable Functions extension currently ships as the [Microsoft.Azure.WebJobs.Extensions.DurableTask](https://www.nuget.org/packages/Microsoft.Azure.WebJobs.Extensions.DurableTask) NuGet package. It can be referenced directly in a Visual Studio project or can be installed using the [Azure Functions Core Tools](https://docs.microsoft.com/azure/azure-functions/functions-run-local) command-line:

```bash
func extensions install -p Microsoft.Azure.WebJobs.Extensions.DurableTask -v 2.0.0
```

Durable Functions is also available in supported [extension bundles](https://docs.microsoft.com/azure/azure-functions/functions-bindings-register#extension-bundles). Note that extension bundles are only supported for non-.NET languages.

## Contributing

Many features of Durable functions have been voluntarily contributed by the community, and we always welcome such contributions. If you are interested in contributing, please take a look at our [CONTRIBUTING](./CONTRIBUTING.md) guide.

## License

This project is licensed under [the MIT License](https://github.com/Azure/azure-webjobs-sdk/blob/master/LICENSE.txt).

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
