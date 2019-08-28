---
name: New release template
about: Template for creating new releases of Durable Functions
title: ''
labels: ''
assignees: cgillum

---

As part of a release, the following items need to be taken care of. For any items that are not applicable, they should be crossed out using the `~~ ~~` markdown syntax with an explanation.

- [ ] Run private scale tests and ensure no regressions
- [ ] Publish updated versions of [DurableTask.Core](https://www.nuget.org/packages/Microsoft.Azure.DurableTask.Core/) and [DurableTask.AzureStorage](https://www.nuget.org/packages/Microsoft.Azure.DurableTask.AzureStorage/) to nuget.org
- [ ] Merge all features and fixes into the `master` branch
- [ ] Update .NET API docs at https://azure.github.io/azure-functions-durable-extension
- [ ] Publish signed version of [Microsoft.Azure.WebJobs.Extensions.DurableTask](https://www.nuget.org/packages/Microsoft.Azure.WebJobs.Extensions.DurableTask/) to nuget.org
- [ ] Update samples to point to the latest nuget packages
- [ ] Create a PR in the [Azure Functions templates repo](https://github.com/Azure/azure-functions-templates) to update templates to the latest version
- [ ] Create a PR in the [Azure Functions bundles repo](https://github.com/Azure/azure-functions-extension-bundles) to update bundles to the latest version
- [ ] Close out or punt remaining GitHub issues for the current milestone
- [ ] Update official docs under https://docs.microsoft.com/en-us/azure/azure-functions/durable ([private docs repo for Microsoft employees](http://github.com/MicrosoftDocs/azure-docs-pr))
- [ ] Publish release notes
- [ ] Post announcement on [App Service Announcements GitHub repo](https://github.com/Azure/app-service-announcements) and Twitter.
