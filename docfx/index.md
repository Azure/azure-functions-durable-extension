# Durable Functions
Durable Functions is an Azure Functions extension for building long-running, stateful function orchestrations in code using C# in a serverless environment.

> [!NOTE]
> Durable Functions is currently in public preview.

## Getting Started
Are you new to Durable Functions? If so, this is the place to start.
* [Overview](~/articles/overview.md)
* [Installation](~/articles/installation.md)
* [Known Issues / FAQ](~/articles/known-issues.md)

## Samples / Walkthroughs
Here are some samples you can study and/or reference. These will help you 1) learn how to write Durable Functions by example and 2) learn the various capabilities of Durable Functions.
* [Function Chaining - Hello Sequence](~/articles/samples/sequence.md)
* [Fan-out/Fan-in - Cloud Backup](~/articles/samples/cloud-backup.md)
* [Stateful Singleton - Counter](~/articles/samples/counter.md)
* [Human Interaction & Timeouts - Phone Verification](~/articles/samples/phone-verification.md)

Also, you can download all the samples as a single function app, which you can download from one of these two links:
* C# Scripts: [DFSampleApp.zip](~/files/DFSampleApp.zip)
* Visual Studio Project: [VSDFSampleApp.zip](~/files/VSDFSampleApp.zip)

Using one of the sample projects is a great way to get up and running quickly and is the recommended way to get started. See the sample / walkthrough links above for specific instructions.

## Topical Guides
Here you will find comprehensive documentation with examples on all of the feature areas. It's *highly* recommended that you read through all of these topics before coding.
* [Bindings](~/articles/topics/bindings.md)
* [Checkpointing & Replay](~/articles/topics/checkpointing-and-replay.md)
* [Instance Management](~/articles/topics/instance-management.md)
* [HTTP APIs](~/articles/topics/http-api.md)
* [Error Handling & Compensation](~/articles/topics/error-handling.md)
* [Diagnostics](~/articles/topics/diagnostics.md)
* [Durable Timers](~/articles/topics/timers.md)
* [External Events](~/articles/topics/external-events.md)
* [Eternal Orchestrations](~/articles/topics/eternal-orchestrations.md)
* [Singleton Orchestrations](~/articles/topics/singletons.md)
* [Sub-Orchestrations](~/articles/topics/sub-orchestrations.md)
* [Task Hubs](~/articles/topics/task-hubs.md)
* [Versioning](~/articles/topics/versioning.md)
* [Performance & Scale](~/articles/topics/perf-and-scale.md)
 
## .NET API Reference
.NET API reference for the attributes and bindings provided by Durable Functions.

### Attributes
* <xref:Microsoft.Azure.WebJobs.OrchestrationTriggerAttribute>
* <xref:Microsoft.Azure.WebJobs.ActivityTriggerAttribute>
* <xref:Microsoft.Azure.WebJobs.OrchestrationClientAttribute>

### Bindings
* <xref:Microsoft.Azure.WebJobs.DurableOrchestrationContext>
* <xref:Microsoft.Azure.WebJobs.DurableActivityContext>
* <xref:Microsoft.Azure.WebJobs.DurableOrchestrationClient>

You can browse all of our public APIs [here](<xref:Microsoft.Azure.WebJobs>).