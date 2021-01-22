---
name: New release template
about: Template for creating new releases of Durable Functions
title: ''
labels: ''
assignees: comcmaho, amdeel, davidmrdavid, bachuv

---

**Prep Release: (assigned to:)**
_Due: <3-business-days-before-release>_
- [ ] Merge all features and fixes into the `dev` branch
- [ ] Publish updated versions of DurableTask.Core and DurableTask.AzureStorage to app-service MyGet feed
- [ ] Update dependencies and increment extension version in the `dev` branch.
- [ ] Publish signed version of Microsoft.Azure.WebJobs.Extensions.DurableTask to app-service MyGet feed
- [ ] Close out or punt remaining GitHub issues for the current milestone


**Validation (assigned to: )**
_Due: <2-business-days-before-release>_
- [ ] Run private performance tests and ensure no regressions
- [ ] Smoke test Functions V1 and Functions V3 .NET apps
- [ ] Smoke test JavaScript and Python apps
- [ ] Check for package size, make sure it's not surprisingly heavier than a previous release


** Release Completion (assigned to: )**:
_Due: <release-deadline>_
- [ ] Push staged package on MyGet to Nuget.org (for Durable Task Framework, you may need to manually update them)
- [ ] Create a PR in the [Azure Functions templates repo](https://github.com/Azure/azure-functions-templates) to update templates to the latest version
- [ ] Create a PR in the [Azure Functions bundles repo](https://github.com/Azure/azure-functions-extension-bundles) to update bundles to the latest version
- [ ] Merge all pending PR docs from `pending_docs.md`
- [ ] Reset `pending_docs.md` and `release_notes.md` in the `dev` branch. You will want to save `release_notes.md` somewhere for when you publish release notes.
- [ ] Merge `dev` into `main`
- [ ] Publish release notes from the pre-reset `release_notes.md`
- [ ] Post announcement on [App Service Announcements GitHub repo](https://github.com/Azure/app-service-announcements) and Twitter.
