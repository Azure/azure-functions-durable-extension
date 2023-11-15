---
name: New release template
about: Template for creating new releases of Durable Functions
title: ''
labels: ''
assignees: davidmrdavid, bachuv, nytiannn

---

**Prep DTFx Release: (assigned to:)**
_Due: <2-3-business-days-before-release>_
- [ ] Check DTFx package versions (either DT-AzureStorage only or if there were Core changes DT-AzureStorage, DT-Core, and DT-ApplicationInsights)
- [ ] Review the [DTFx Dependabot vulnerability alerts](https://github.com/Azure/durabletask/security/dependabot) and address them. Note: code samples / test projects _may_ be excluded from this check.
- [ ] Delete DTFx test packages from the [ADO feed](https://dev.azure.com/durabletaskframework/Durable%20Task%20Framework%20CI/_artifacts/feed/durabletask).
- [ ] Run the [DTFx release pipeline](https://durabletaskframework.visualstudio.com/Durable%20Task%20Framework%20CI/_build?definitionId=21) ([defined here](https://github.com/Azure/durabletask/blob/main/azure-pipelines-release.yml)) to obtain new packages.
- [ ] Publish DTFx packages to the [ADO feed](https://dev.azure.com/durabletaskframework/Durable%20Task%20Framework%20CI/_artifacts/feed/durabletask) for testing.
- [ ] Keep branch `azure-storage-v12` updated with branch `main`.

** Prep `durabletask-dotnet` Release: (assigned to: )**
_Due: <2-3-business-days-before-release>_
- [] Check for changes in [durabletask-dotnet](https://github.com/microsoft/durabletask-dotnet) since the last extension release. If the upcoming extension releases has a dependency on them, release the dependencies first. In particular, check for updates to `Microsoft.DurableTask.Grpc`.


**Prep Release (assigned to: )**
_Due: <2-business-days-before-release>_
- [ ] Update Durable Functions references (Analyzer? DTFx?) and check current version.
- [ ] Locally, run `dotnet list package --vulnerable` to ensure the release is not affected by any vulnerable dependencies.
- [ ] Review the [Dependabot vulnerability alerts](https://github.com/Azure/azure-functions-durable-extension/security/dependabot) and address them. Note: code samples / test projects _may_ be excluded from this check.
- [ ] Add the Durable Functions package to the [ADO test feed](https://dev.azure.com/durabletaskframework/Durable%20Task%20Framework%20CI/_artifacts/feed/durabletask-test).
- [ ] Check for package size, make sure it's not surprisingly heavier than a previous release.
- [ ] Merge (**choose create a merge commit, NOT squash merge**) dev into main. Person performing validation must approve PR.
- [ ] Keep branch `v3.x` updated with branch `dev`. Do not merge PRs that are specific to Durable Functions v2.

**Validation**
_Due: <1-business-days-before-release>_
- [ ] Run private performance tests and ensure no regressions. **(assigned to: )**
- [ ] Smoke test Functions V1, Functions V2, and Functions V3 .NET apps. **(assigned to: )**

**DTFx Release Completion (assigned to: )**
_Due: <release-deadline>_
- [ ] Upload DTFx packages to NuGet (directly to nuget.org).
- [ ] Delete `Microsoft.DurableTask.Sidecar.Protobuf` from MyGet, and publish it to NuGet _iff_ it was updated as an Extension dependency. 
- [ ] Publish release notes for DTFx.
- [ ] Patch increment DTFx packages that were released (either DT-AzureStorage only or if there were Core changes DT-AzureStorage, DT-Core, and DT-ApplicationInsights)

**Release Completion (assigned to: )**
_Due: <release-deadline>_
- [ ] Delete Durable Functions packages from the [ADO test feed](https://dev.azure.com/durabletaskframework/Durable%20Task%20Framework%20CI/_artifacts/feed/durabletask-test).
- [ ] Run the [Durable Functions release pipeline](https://dev.azure.com/durabletaskframework/Durable%20Task%20Framework%20CI/_build?definitionId=23) and select `main` as the branch.
- [ ] Add the Durable Functions package to the [ADO feed](https://dev.azure.com/durabletaskframework/Durable%20Task%20Framework%20CI/_artifacts/feed/durabletask) using [this pipeline](https://dev.azure.com/durabletaskframework/Durable%20Task%20Framework%20CI/_release?_a=releases&view=mine&definitionId=11).
- [ ] Upload the Durable Functions package to NuGet (directly to nuget.org).
- [ ] Create a PR in the [Azure Functions templates repo](https://github.com/Azure/azure-functions-templates) targeting branch `dev` to update all references of "Microsoft.Azure.WebJobs.Extensions.DurableTask" (search for this string in the code) to the latest version.
- [ ] _if and only if this is a new major release_, Create a PR in the [Azure Functions bundles repo](https://github.com/Azure/azure-functions-extension-bundles) to update bundles to the latest version .
- [ ] Merge all pending PR docs from `pending_docs.md.`
- [ ] Reset `pending_docs.md` and `release_notes.md` in the `dev` branch. You will want to save `release_notes.md` somewhere for when you publish release notes.
- [ ] Publish release notes from the pre-reset `release_notes.md.`
- [ ] Post announcement on Twitter (Chris).
- [ ] Increment Durable Functions patch version.
