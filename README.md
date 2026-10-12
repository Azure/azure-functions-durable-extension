# Durable Functions

|Branch|Status|
|---|---|
|dev|[![Build Status](https://durabletaskframework.visualstudio.com/Durable%20Task%20Framework%20CI/_apis/build/status/Azure.azure-functions-durable-extension?branchName=dev)](https://durabletaskframework.visualstudio.com/Durable%20Task%20Framework%20CI/_build/latest?definitionId=15&branchName=dev)|

Durable Functions is an extension that helps developers build reliable, stateful apps on the [Azure Functions](https://functions.azure.com) platform.

This extension adds three new types functions to the Azure Functions family:

* **[Orchestrator functions](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-orchestrations)**: Long-running, reliable workflow functions written in code that schedule and coordinate other functions.
* **[Activity functions](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-types-features-overview#activity-functions)**: Stateless functions that are the basic unit of work in a durable function orchestration.
* **[Entity functions](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-entities)**: Stateful [actor](https://en.wikipedia.org/wiki/Actor_model)-like functions that have identities and store state explicitly.

Durable Functions can run anywhere that Azure Functions can run, including in the Azure Functions "Serverless" [Consumption plan](https://docs.microsoft.com/azure/azure-functions/functions-scale#consumption-plan), the [Elastic Premium plan](https://docs.microsoft.com/azure/azure-functions/functions-scale#premium-plan), on [Kubernetes](https://docs.microsoft.com/azure/azure-functions/functions-kubernetes-keda), or even locally for development using [Visual Studio](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-create-first-csharp) or [Visual Studio Code](https://docs.microsoft.com/azure/azure-functions/functions-develop-vs-code).

📑 **[Official documentation](https://docs.microsoft.com/azure/azure-functions/durable/)** 📑

For a more detailed overview, including examples of what you can do with Durable Functions, see our [What is Durable Functions?](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-overview) article.

## NuGet Packages

Durable Functions updates are published as NuGet packages.

Package Name | NuGet
---|---
Microsoft.Azure.WebJobs.Extensions.DurableTask | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Azure.WebJobs.Extensions.DurableTask.svg)](https://www.nuget.org/packages/Microsoft.Azure.WebJobs.Extensions.DurableTask)
Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers (C# only) | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers.svg)](https://www.nuget.org/packages/Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers)
Microsoft.Azure.Functions.Worker.Extensions.DurableTask | [![NuGet](https://img.shields.io/nuget/v/Microsoft.Azure.Functions.Worker.Extensions.DurableTask.svg)](https://www.nuget.org/packages/Microsoft.Azure.Functions.Worker.Extensions.DurableTask)


## Language support

Durable Functions supports a subset of languages supported by Azure Functions:

| Language   | Status | Repo |
|------------|------------------|-|
| C#         | Generally available - [get started](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-create-first-csharp) | [Azure/azure-functions-durable-extension](https://github.com/Azure/azure-functions-durable-extension) |
| JavaScript | Generally available - [get started](https://docs.microsoft.com/azure/azure-functions/durable/quickstart-js-vscode) | [Azure/azure-functions-durable-js](https://github.com/Azure/azure-functions-durable-js) |
| Python     | Generally available - [get started](https://docs.microsoft.com/azure/azure-functions/durable/quickstart-python-vscode) | [Azure/azure-functions-durable-python](https://github.com/Azure/azure-functions-durable-python) |
| PowerShell | Generally available - [get started](https://docs.microsoft.com/azure/azure-functions/durable/quickstart-powershell-vscode) | [Azure/azure-functions-powershell-worker](https://github.com/Azure/azure-functions-powershell-worker) |
| Java       | Generally available - [get started](https://learn.microsoft.com/azure/azure-functions/durable/quickstart-java?tabs=bash&pivots=create-option-vscode) | [Microsoft/durabletask-java](https://github.com/microsoft/durabletask-java) |

Each language has its own language-specific SDK and programming model. Regardless of which language you use, the extension in this repo must be installed to enable the Durable Functions triggers.

Samples for each SDK may be found in their respective repos, usually under a "/samples" directory. For example, the JavaScript samples may be found [here](https://github.com/Azure/azure-functions-durable-js/tree/dev/samples).

## Installation

The Durable Functions NuGet package can be referenced directly in a Visual Studio project or can be installed using the [Azure Functions Core Tools](https://docs.microsoft.com/azure/azure-functions/functions-run-local) command-line:

```bash
func extensions install -p Microsoft.Azure.WebJobs.Extensions.DurableTask -v <latest version on Nuget.org>
```

Durable Functions is also available in supported [extension bundles](https://docs.microsoft.com/azure/azure-functions/functions-bindings-register#extension-bundles). Note that extension bundles are only supported for non-.NET languages.

## Backend completion failures

A successful orchestrator or activity invocation does not mean its results have been committed to orchestration history. The storage provider completes the work item after the function invocation returns. A permanent failure at this stage can mark the orchestration `Failed` even if the last invocation log says the function completed or the orchestrator yielded. This applies across languages.

For Durable Task Scheduler (DTS), the completion request includes new history, scheduled activity inputs, timers, messages, and custom status. Individually small activity results can still produce an oversized request when an orchestrator schedules another activity with their aggregated results. The provider can replace the oversized episode with a terminal, non-retriable failure. Events from the rejected episode might therefore be absent from persisted history despite successful activity invocation logs.

Monitor persisted orchestration status and failure details, not just function invocation logs or invocation-level lifecycle notifications. A failure generated during backend completion occurs outside the orchestrator's execution boundary and cannot currently be caught by compensation code inside that same orchestrator. Backend-generated failures do not automatically run the extension's normal orchestrator-failure lifecycle notifications. Applications that require notification or compensation for these failures need an independent observer of terminal orchestration status.

DTS provider diagnostics use the `DurableTask.AzureManagedBackend` logging category. Keep Error-level logs enabled for this category in `host.json`:

```json
{
  "version": "2.0",
  "logging": {
    "logLevel": {
      "DurableTask.AzureManagedBackend": "Error"
    }
  }
}
```

Use a provider or extension bundle version that includes completion-failure reporting; changing log levels alone cannot make an older provider emit a missing error log. The `OrchestrationCompletionRejected` Error event identifies the instance ID, execution ID and function name when available, task hub, original serialized request size in bytes, limit type and size in bytes, and rejection reason. It does not include application payload contents or storage exception response bodies. Application Insights visibility also depends on the host's telemetry configuration and sampling.

`SizeLimitType` distinguishes the aggregate `CompletionRequest` limit from the configured `ExternalizedPayload` limit for each individual payload. The latter is not an aggregate request limit. Permanent externalization failures can also result from Blob Storage access errors, so use the rejection reason to distinguish storage access failures from size violations.

This event reports rejection of the original completion, not proof that the replacement terminal failure was committed. Inspect persisted status and any subsequent backend submission errors to determine the final outcome.

To reduce request size, batch the work or configure [supported large-payload externalization](https://learn.microsoft.com/azure/durable-task/scheduler/durable-task-scheduler-large-payloads). Externalization does not remove all backend request limits.

## Contributing

Many features of Durable Functions have been voluntarily contributed by the community, and we always welcome such contributions. If you are interested in contributing, please take a look at our [CONTRIBUTING](./CONTRIBUTING.md) guide.

## Publications

Durable Functions is developed in collaboration with Microsoft Research. As a result, the Durable Functions team actively produces research papers and artifacts; these include:

* [Durable Functions: Semantics for Stateful Serverless](https://www.microsoft.com/en-us/research/uploads/prod/2021/10/DF-Semantics-Final.pdf) _(OOPSLA'21)_
* [Netherite: Efficient Execution of Serverless Workflows](https://www.microsoft.com/en-us/research/uploads/prod/2022/07/p1591-burckhardt.pdf) _(VLDB'22)_

## License

This project is licensed under [the MIT License](https://github.com/Azure/azure-webjobs-sdk/blob/master/LICENSE.txt).

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
