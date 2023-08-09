---
name: New release template
about: Template for creating new releases of Durable Functions
title: ''
labels: ''
assignees: amdeel, davidmrdavid, bachuv, nytiannn

---

**Prep DTFx Release: (assigned to:)**
_Due: <2-3-business-days-before-release>_
- [ ] Check DTFx package versions (either DT-AzureStorage only or if there were Core changes DT-AzureStorage, DT-Core, DT-Emulator, and DT-Redis)
- [ ] Review the [DTFx Dependabot vulnerability alerts](https://github.com/Azure/durabletask/security/dependabot) and address them. Note: code samples / test projects _may_ be excluded from this check.
- [ ] Delete DTFx test packages from staging myget.
- [ ] Run the [DTFx release pipeline](https://durabletaskframework.visualstudio.com/Durable%20Task%20Framework%20CI/_build?definitionId=21) ([defined here](https://github.com/Azure/durabletask/blob/main/azure-pipelines-release.yml)) to obtain new packages.
- [ ] Publish DTFx packages to staging myget (https://www.myget.org/feed/Packages/azure-appservice) for testing.

**Prep Release (assigned to: )**
_Due: <2-business-days-before-release>_
- [ ] Update Durable Functions references (Analyzer? DTFx?) and check current version.
- [ ] Locally, run `dotnet list package --vulnerable` to ensure the release is not affected by any vulnerable dependencies.
- [ ] Review the [Dependabot vulnerability alerts](https://github.com/Azure/azure-functions-durable-extension/security/dependabot) and address them. Note: code samples / test projects _may_ be excluded from this check.
- [ ] Add Durable Functions package to myget staging feed.
- [ ] Check for package size, make sure it's not surprisingly heavier than a previous release.
- [ ] Merge dev into main. Person performing validation must approve PR. Important: Merge NOT Squash merge.

**Validation**
_Due: <1-business-days-before-release>_
- [ ] Run private performance tests and ensure no regressions. **(assigned to: )**
- [ ] Smoke test Functions V1, Functions V2, and Functions V3 .NET apps. **(assigned to: )**
- [ ] Smoke test JavaScript and Python apps. **(assigned to: )**

**DTFx Release Completion (assigned to: )**
_Due: <release-deadline>_
- [ ] Publish DTFx packages to nuget (directly to nuget.org).
- [ ] Delete `Microsoft.DurableTask.Sidecar.Protobuf` from MyGet, and publish it to NuGet _iff_ it was updated as an Extension dependency. 
- [ ] Publish release notes for DTFx.
- [ ] Patch increment DTFx packages that were released (either DT-AzureStorage only or if there were Core changes DT-AzureStorage, DT-Core, DT-Emulator, and DT-Redis)

**Release Completion (assigned to: )**
_Due: <release-deadline>_
- [ ] Delete Durable Functions packages from myget.
- [ ] Run Durable Functions release pipeline.
- [ ] Push myget package to nuget (nuget.org extensions package option).
- [ ] Create a PR in the [Azure Functions templates repo](https://github.com/Azure/azure-functions-templates) targeting branch `dev` to update all references of "Microsoft.Azure.WebJobs.Extensions.DurableTask" (search for this string in the code) to the latest version.
- [ ] _if and only if this is a new major release_, Create a PR in the [Azure Functions bundles repo](https://github.com/Azure/azure-functions-extension-bundles) to update bundles to the latest version .
- [ ] Merge all pending PR docs from `pending_docs.md.`
- [ ] Reset `pending_docs.md` and `release_notes.md` in the `dev` branch. You will want to save `release_notes.md` somewhere for when you publish release notes.
- [ ] Publish release notes from the pre-reset `release_notes.md.`
- [ ] Post announcement on [App Service Announcements GitHub repo](https://github.com/Azure/app-service-announcements) and Twitter (Chris).
- [ ] Increment Durable Functions patch version.
