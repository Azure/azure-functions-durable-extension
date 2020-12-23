---
name: New release template
about: Template for creating new releases of Durable Functions
title: ''
labels: ''
assignees: comcmaho, amdeel, davidmrdavid, bachuv

---

**Prep Release: (assigned to:)**
_Due: <two-businessdays-before-release>_
- [ ] Merge all features and fixes into the `main` branch
- [ ] Publish updated versions of DurableTask.Core and DurableTask.AzureStorage to app-service MyGet feed
- [ ] Increment extension version
- [ ] Publish signed version of Microsoft.Azure.WebJobs.Extensions.DurableTask to app-service MyGet feed

**Validation (assigned to: )**
_Due: <1-businessdays-before-release>_
- [ ] Run private performance tests and ensure no regressions
- [ ] Smoke test Functions V1 and Functions V3 .NET apps
- [ ] Smoke test JavaScript and Python apps


** Release Completion (assigned to: )**:
_Due: <release-deadline>_
- [ ] Push staged package on MyGet to Nuget.org (for Durable Task Framework, you may need to manually update them)
- [ ] Create a PR in the [Azure Functions templates repo](https://github.com/Azure/azure-functions-templates) to update templates to the latest version
- [ ] Create a PR in the [Azure Functions bundles repo](https://github.com/Azure/azure-functions-extension-bundles) to update bundles to the latest version
- [ ] Close out or punt remaining GitHub issues for the current milestone
- [ ] Merge all pending PR docs, and reset pending_docs.md
- [ ] Publish release notes and reset release_notes.md
- [ ] Remerge to master now that pending docs and release notes are cleared?
- [ ] Post announcement on [App Service Announcements GitHub repo](https://github.com/Azure/app-service-announcements) and Twitter.
