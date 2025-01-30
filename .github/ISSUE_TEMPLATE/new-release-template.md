---
name: New release template
about: Template for creating new releases of Durable Functions
title: ''
labels: ''
assignees: bachuv, nytiannn, andystaples

---

**Prep DTFx Release: (assigned to:)**
_Due: <2-3-business-days-before-release>_
- [ ] Check DTFx package versions (either DT-AzureStorage only or if there were Core changes DT-AzureStorage, DT-Core, and DT-ApplicationInsights)
- [ ] Review the [DTFx Dependabot vulnerability alerts](https://github.com/Azure/durabletask/security/dependabot) and address them. Note: code samples / test projects _may_ be excluded from this check.
- [ ] Run the [DTFx release pipeline](https://azfunc.visualstudio.com/internal/_build?definitionId=640) ([defined here](https://github.com/Azure/durabletask/blob/main/eng/ci/official-build.yml)) to obtain new packages.
- [ ] Publish DTFx packages to the [ADO test feed](https://azfunc.visualstudio.com/internal/_artifacts/feed/durabletask-test) for testing.

**Prep DotNet Isolated SDK Release: (assigned to:)**
_Due: <2-3-business-days-before-release>_
- [ ] If there were DTFx.Core changes, check its reference version [here](https://github.com/microsoft/durabletask-dotnet/blob/c838535adb6aedb6671cf193389ce63a6b4a9b24/src/Abstractions/Abstractions.csproj#L10). If updates are required, document the changes in [release notes](https://github.com/microsoft/durabletask-dotnet/blob/c838535adb6aedb6671cf193389ce63a6b4a9b24/src/Abstractions/RELEASENOTES.md).
- [ ] Check dotnet isolated SDK versions [here](https://github.com/microsoft/durabletask-dotnet/blob/c838535adb6aedb6671cf193389ce63a6b4a9b24/eng/targets/Release.props#L20). If updated, document the changes in the [change logs](https://github.com/microsoft/durabletask-dotnet/blob/c838535adb6aedb6671cf193389ce63a6b4a9b24/CHANGELOG.md).
- [ ] Run pipeline [Release .Net out-of-proc SDK](https://azfunc.visualstudio.com/internal/_build?definitionId=657) to create the new package and publish it to the ADO feed for testing.

**Prep WebJobs and Worker Extensions Release (assigned to: )**
_Due: <2-business-days-before-release>_
- [ ] Update DTFx packages and Analyzer versions at WebJobs.Extensions.Durabletask.csproj and check if we need to update the WebJobs.Extensions.Durabletask version.
- [ ] Locally, run `dotnet list package --vulnerable` to ensure the release is not affected by any vulnerable dependencies.
- [ ] Review the [Dependabot vulnerability alerts](https://github.com/Azure/azure-functions-durable-extension/security/dependabot) and address them. Note: code samples / test projects _may_ be excluded from this check.
- [ ] Check for package size, make sure it's not surprisingly heavier than a previous release.
- [ ] Update .NET Isolated SDK version at Worker.Extensions.Durabletask.csproj and check if we need to update the Worker.Extensions.Durabletask version.
- [ ] Run [the extension release pipeline](https://azfunc.visualstudio.com/internal/_build?definitionId=673) to create the new WebJobs.Extensions.Durabletask and Worker.Extensions.Durabletask packages and add them to the [ADO test feed].(https://azfunc.visualstudio.com/internal/_artifacts/feed/durabletask-internal) for testing.
- [ ] Cherry-pick any PRs that need to be in the `v2.x` branch

**Validation**
_Due: <1-business-days-before-release>_
- [ ] Run private performance tests and ensure no regressions. **(assigned to: )**
- [ ] Smoke test .NET isolated apps. **(assigned to: )** - check that the correct version of the webjobs extension is loaded by going to bin\Debug\net8.0\.azurefunctions\Microsoft.Azure.WebJobs.Extensions.DurableTask.dll, right click on Properties, go to the Details tab and check the version
- [ ] Merge (**choose create a merge commit, NOT squash merge**) dev into main. Person performing validation must approve PR.

**DTFx Release Completion (assigned to: )**
_Due: <release-deadline>_
- [ ] Add the DTFx packages to the [ADO feed](https://azfunc.visualstudio.com/internal/_artifacts/feed/durabletask-internal)
- [ ] Upload DTFx packages to NuGet (directly to nuget.org). 
- [ ] Publish release notes for DTFx.
- [ ] Patch increment DTFx packages that were released (either DT-AzureStorage only or if there were Core changes DT-AzureStorage, DT-Core, and DT-ApplicationInsights) in WebJobs.Extensions.DurableTask.csproj

**DotNet Isolated SDK Release Completion: (assigned to:)**
_Due: <release-deadline>_
- [ ] Add the .NET isolated SDK packages to the [ADO feed](https://azfunc.visualstudio.com/internal/_artifacts/feed/durabletask-internal)
- [ ] Upload .NET isolated SDK packages to NuGet (directly to nuget.org).
- [ ] Publish release notes in the durable-dotnet repo.

**Release Completion (assigned to: )**
_Due: <release-deadline>_
- [ ] Run the [Durable Functions release pipeline](https://azfunc.visualstudio.com/internal/_build?definitionId=673) and select `dev` or `v2.x` as the branch. Choose `dev` if you are making a v3.x release, otherwise choose `v2.x` for a v2.x release.
- [ ] Add the Durable Functions packages to the [ADO feed](https://azfunc.visualstudio.com/internal/_artifacts/feed/durabletask-internal)
- [ ] Upload the Durable Functions package to NuGet (directly to nuget.org).
- [ ] Upload .NET Isolated worker extension package to NuGet (directly to nuget.org).
- [ ] Create a PR in the [Azure Functions templates repo](https://github.com/Azure/azure-functions-templates) targeting branch `dev` to update all references of "Microsoft.Azure.WebJobs.Extensions.DurableTask" (search for this string in the code) to the latest version.
- [ ] _if and only if this is a new major release_, Create a PR in the [Azure Functions bundles repo](https://github.com/Azure/azure-functions-extension-bundles) to update bundles to the latest version .
- [ ] Publish release notes.
