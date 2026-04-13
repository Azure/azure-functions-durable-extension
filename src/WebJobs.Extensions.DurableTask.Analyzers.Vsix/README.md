# WebJobs.Extensions.DurableTask.Analyzers.Vsix

This is a **Visual Studio Extension (VSIX)** project that packages the [Durable Functions Roslyn Analyzers](../WebJobs.Extensions.DurableTask.Analyzers/) for distribution via the Visual Studio Marketplace.

> [!NOTE]
> This package is no longer being actively maintained. It is only relevant to the [.NET in-process model](https://learn.microsoft.com/en-us/azure/azure-functions/functions-dotnet-class-library?tabs=v4%2Ccmd), which is being deprecated in favor of the [.NET isolated worker model](https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide). Support for the in-process model will end on November 10, 2026.

| | |
|---|---|
| **Project Type** | VSIX (Visual Studio Extension) |
| **Target Framework** | .NET Framework 4.6.2 |
| **Minimum VS Version** | Visual Studio 2017 (15.0) |

## Where This Code Runs

The VSIX installs the analyzer assembly into **Visual Studio** as a MEF component and Roslyn analyzer. Once installed, it provides real-time Durable Functions diagnostics for any C# project without requiring the analyzer NuGet package to be added to the project.

## Relationship to the NuGet Analyzer

The same analyzer code can be consumed in two ways:

| Method | Details |
|---|---|
| **NuGet package** (recommended) | [`Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers`](https://www.nuget.org/packages/Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers) — included per-project and works in CI/CD. Automatically pulled in by the main Durable Functions extension package. |
| **VSIX extension** (this project) | Installed globally in Visual Studio. Useful for IDE-wide diagnostics without modifying project files, but does not run in CI/CD builds. |

## Build Notes

This project contains no C# source code — it is purely a packaging wrapper. It uses the legacy `.csproj` format (non-SDK-style) because the VS SDK VSIX tooling requires it.
