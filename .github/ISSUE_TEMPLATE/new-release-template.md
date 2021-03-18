---
name: New release template
about: Template for creating new releases of Durable Functions
title: ''
labels: ''
assignees: comcmaho, amdeel, davidmrdavid, bachuv

---

**Prep DTFx Release: (assigned to:)**
_Due: <2-3-business-days-before-release>_
- [ ] Check DTFx package versions (either DT-AzureStorage only or if there were Core changes DT-AzureStorage, DT-Core, DT-Emulator, and DT-Redis)
- [ ] Delete DTFx test packages from staging myget.
- [ ] Update OneBranch durabletask repo, run signing pipeline (using these notes:  [Durable Task Framework Build Pipeline] (https://microsoft.sharepoint.com/teams/AzureWebjobs/_layouts/OneNote.aspx?id=%2Fteams%2FAzureWebjobs%2FSiteAssets%2FAzureWebjobs%20Notebook&wd=target%28Planning%2FFunctions%20Post-GA.one%7CA43CF112-7272-481A-B23E-9AA5CA8EEE06%2FDurable%20Task%20Framework%20Build%20Pipeline%7CD0946823-6FB0-44E3-A57F-E252617B69CD%2F%29
onenote:https://microsoft.sharepoint.com/teams/AzureWebjobs/SiteAssets/AzureWebjobs%20Notebook/Planning/Functions%20Post-GA.one#Durable%20Task%20Framework%20Build%20Pipeline&section-id={A43CF112-7272-481A-B23E-9AA5CA8EEE06}&page-id={D0946823-6FB0-44E3-A57F-E252617B69CD}&end) ).
- [ ] Publish DTFx packages to staging myget for testing. (either DT-AzureStorage only or if there were Core changes DT-AzureStorage, DT-Core, DT-Redis, and DT-Emulator)

**Prep Release (assigned to: )**
_Due: <2-business-days-before-release>_
- [ ] Update Durable Functions references (Analyzer? DTFx?) and check current version.
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
- [ ] Publish release notes for DTFx.
- [ ] Patch increment DTFx packages that were released (either DT-AzureStorage only or if there were Core changes DT-AzureStorage, DT-Core, DT-Emulator, and DT-Redis)

**Release Completion (assigned to: )**
_Due: <release-deadline>_
- [ ] Delete Durable Functions packages from myget.
- [ ] Run Durable Functions release pipeline.
- [ ] Push myget package to nuget (nuget.org extensions package option).
- [ ] Create a PR in the [Azure Functions templates repo](https://github.com/Azure/azure-functions-templates) to update templates to the latest version.
- [ ] Create a PR in the [Azure Functions bundles repo](https://github.com/Azure/azure-functions-extension-bundles) to update bundles to the latest version.
- [ ] Merge all pending PR docs from `pending_docs.md.`
- [ ] Reset `pending_docs.md` and `release_notes.md` in the `dev` branch. You will want to save `release_notes.md` somewhere for when you publish release notes.
- [ ] Merge `dev` into `main.`
- [ ] Publish release notes from the pre-reset `release_notes.md.`
- [ ] Post announcement on [App Service Announcements GitHub repo](https://github.com/Azure/app-service-announcements) and Twitter (Chris).
- [ ] Increment Durable Functions patch version.
