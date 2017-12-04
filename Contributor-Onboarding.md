# Contributor Onboarding Guide

🚧 This document is still a WIP 🚧 

## General

 - The goal of this guide is to help you start contributing to Durable Functions.

## Pre-reqs

 - OS
    - Windows 10 (suggested)
    - Mac OS X/Linux
 - Language runtimes
    - .NET Core 2.0
 - Editor
    - Visual Studio 2017 (recommended)
    - VS Code 
 - Misc tools (suggested)
    - git - source control
    - [Azure Storage Emulator](https://docs.microsoft.com/azure/storage/storage-use-emulator) or real Azure Storage Account

## Testing code changes locally (Visual Studio) 

Durable Functions are distributed as a NuGet package. So in order to test your changes, you need to integrate it in a function. For that you need the following steps:

1. Modify the code.
2. Build the project and this will create a new NuGet package with your changes.
3. Create a local NuGet source on your dev machine - e.g. C:\LocalNuGet.
4. Then add the local NuGet source as a NuGet package source in Visual Studio. 
5. Add the newly built Durable Functions NuGet package to the local NuGet source. The command to add packages to this location is nuget **add package.nupkg -Source C:\LocalNuGet** . 
6. Add the symbols file as well to be able to debug your modifications while debugging the function code. 
7. Update your local NuGet cache (**%USERPROFILE%\.nuget\packages**) with the newest version of the Durable Functions NuGet package.
8. In Visual Studio, add the new Durable Functions NuGet package from your local NuGet source to a function. 
9. Run Azure Storage Emulator 5.2.
10. Run the function and debug.

## Running the tests (Visual Studio) 

1. Build the project and Visual Studio will identify all the tests in the solution.
2. Set an environment variable named **AzureWebJobsStorage** to a connection string e.g. to the storage emulator.
3. Set an environment variable named **AzureWebJobsDashboard** with the same value as **AzureWebJobsStorage**.
4. Run Azure Storage Emulator 5.2.
5. Run the unit tests via Visual Studio Test Explorer by selecting "Run All"

## Change flow

The general flow for making a change to the script host is:
1. 🍴 Fork the repo (add the fork via `git remote add me <clone url here>`
2. 🌳 Create a branch for your change (generally use dev) (`git checkout -b my-change`)
3. 🛠 Make your change
4. ✔️ Test your changes
5. ⬆️ Push your changes to your fork (`git push me my-change`)
6. 💌 Open a PR to the dev branch
7. 📢 Address feedback and make sure tests pass (yes even if it's an "unrelated" test failure)
8. 📦 [Rebase](https://git-scm.com/docs/git-rebase) your changes into a meaningful commits (`git rebase -i HEAD~N` where `N` is commits you want to squash)
9. :shipit: Rebase and merge (This will be done for you if you don't have contributor access)
10. ✂️ Delete your branch (optional)

## Getting help

 - Leave comments on your PR and @ people for attention
 - [@AzureFunctions](https://twitter.com/AzureFunctions) on twitter
 - (MSFT Internal only) Functions Dev teams channel & email
