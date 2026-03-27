# GitHub Copilot Instructions

This repository contains the C# implementation of the **Durable Functions** Azure Functions extension. It provides orchestrator, activity, and entity function triggers for building reliable, stateful serverless apps on Azure Functions.

When contributing to this repository, please follow these guidelines.

---

## C# Code Guidelines

The following rules apply to all `*.cs` files in this repository.

### Copyright Header

Every `*.cs` file must begin with the following copyright notice (evidence: `src/WebJobs.Extensions.DurableTask/DurableTaskExtension.cs`, `src/WebJobs.Extensions.DurableTask/AsyncLock.cs`, and all other source files):

```csharp
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
```

> Note: This repo uses `.NET Foundation` (not `Microsoft Corporation`) as the copyright holder. This is the correct header for this repo.

### XML Documentation

All public classes and methods must have XML documentation comments (evidence: `DurableTaskExtension.cs` class-level `<summary>`, constructor `<summary>` and `<param>` tags).

### Member Access

Use `this.` when accessing instance fields and methods (evidence: `AsyncLock.cs` — `this.semaphore.WaitAsync()`, `this.asyncLock.Release()`; `DurableTaskExtension.cs` — `this.taskHubLock`, `this.durabilityProviderFactory`).

### Async Methods

All `async` methods must use the `Async` suffix in their name (evidence: `AsyncLock.cs` — `AcquireAsync()`; `DurableTaskExtension.cs` implements `IAsyncConverter` with `Async`-suffixed method).

### Sealed Private Classes

Private inner classes that do not serve as base classes must be declared `sealed` (evidence: `AsyncLock.cs` — `internal sealed class AsyncLock`; the `Releaser` struct inside it).

### No Breaking Changes

No change should introduce a breaking change unless explicitly documented in the PR summary, a linked GitHub issue, or a GitHub discussion. Breaking change reference: https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/breaking-change-rules.md (evidence: stated in `CONTRIBUTING.md` change flow — "Address feedback and make sure tests pass").

### Explicit Types

Prefer defining variables using explicit types rather than `var`, to help readers understand the types involved (evidence: `AzureStorageDurabilityProviderFactoryTests.cs` — all test variables declared with explicit type; referenced in implementation hints for sample code conventions carried over from the sibling `durabletask-dotnet` repo).

---

## SDK and Build Requirements

- **SDK:** .NET 10 SDK version **10.0.102 or later** is required (evidence: `global.json` — `"version": "10.0.103"`, `"rollForward": "latestFeature"`; `CONTRIBUTING.md` — "The .NET 10 SDK is required because the project multi-targets `net8.0` and `net10.0`").
- **Build tool:** `dotnet` CLI (Visual Studio 2022 also supported per `CONTRIBUTING.md`).
- **Verify SDK:** Run `dotnet --version` and confirm it is 10.0.1xx or later.
- **Target frameworks:** The solution multi-targets `net8.0` and `net10.0`. The .NET 10 SDK can build all earlier target frameworks; the .NET 8 SDK cannot build `net10.0` targets (evidence: `CONTRIBUTING.md`).

---

## Package Structure

This repo produces multiple NuGet packages (evidence: `README.md` NuGet Packages table):

| Package | Source directory |
|---------|-----------------|
| `Microsoft.Azure.WebJobs.Extensions.DurableTask` | `src/WebJobs.Extensions.DurableTask/` |
| `Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers` | `src/WebJobs.Extensions.DurableTask.Analyzers/` |
| `Microsoft.Azure.Functions.Worker.Extensions.DurableTask` | `src/Worker.Extensions.DurableTask/` |
| `Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale` | `src/Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale/` |
| `DurableFunctions.TypedInterfaces` | `src/DurableFunctions.TypedInterfaces/` |

When adding a feature that affects multiple packages, make sure all relevant packages are updated consistently. Do not introduce an API in one package that creates an inconsistency with another.

---

## Test Guidelines

### Test Location and Framework

All tests are located under the `test/` directory (evidence: `CONTRIBUTING.md` — "All tests for Durable Functions are found in `test/Common`"). Tests are written using the **xUnit** framework (evidence: `CONTRIBUTING.md`; `AzureStorageDurabilityProviderFactoryTests.cs` — `using Xunit;`).

Use **Moq** for mocking objects where appropriate (evidence: `AzureStorageDurabilityProviderFactoryTests.cs` — `using Moq;`, `new Mock<INameResolver>().Object`).

### Required Test Trait Attributes

Every test that should run in CI **must** be decorated with one of the following trait attributes (evidence: `CONTRIBUTING.md` — "In order to run any tests you write in our CI pipeline, the test must have one of the following attributes"):

```csharp
[Trait("Category", PlatformSpecificHelpers.TestCategory)]
[Trait("Category", PlatformSpecificHelpers.TestCategory + "_BVT")]
[Trait("Category", PlatformSpecificHelpers.FlakeyTestCategory)]
```

Use `TestCategory` for standard tests, `TestCategory + "_BVT"` for build verification tests, and `FlakeyTestCategory` for known-flaky tests. Avoid writing flaky tests.

### Running Tests

Set the `AzureWebJobsStorage` environment variable to a real Azure General Purpose Storage Account connection string before running tests (evidence: `CONTRIBUTING.md` — "Set an environment variable named **AzureWebJobsStorage** set to an Azure General Purpose Storage Account connection string"). Using the local storage emulator (`UseDevelopmentStorage=true`) is discouraged because performance and reliability are severely impacted.

### Test Quality

- Add `// Arrange`, `// Act`, and `// Assert` comments in each test.
- Validate that each test exercises the target production code path — do not write tests that only call and verify mocks without involving the code under test.
- Avoid excessive comments; prefer clear, self-explanatory test code.
- Follow the patterns of the existing tests in the same project or class.

---

## Sample Guidelines

Samples are located in the `samples/` directory (evidence: `samples/` directory contains `functionapp-csharp`, `precompiled`, `distributed-tracing`, etc.).

When adding a new sample:

- The sample should be a standalone Azure Functions project in a subdirectory of `samples/`.
- The directory name should match the project name.
- The directory must contain a `README.md` explaining what the sample does and how to run it. Follow the format of existing sample READMEs.
- The `.csproj` file name should match the directory name.
- Add the new sample to the `samples/Samples.sln` solution file (evidence: `samples/Samples.sln` is present).

Sample code must follow these rules:

- Configuration settings (connection strings, endpoints, keys) must be read from environment variables. Example:
  ```csharp
  string storageConnection = Environment.GetEnvironmentVariable("AzureWebJobsStorage")
      ?? throw new InvalidOperationException("AzureWebJobsStorage is not set.");
  ```
- Environment variable names must use `UPPER_SNAKE_CASE` convention (evidence: `CONTRIBUTING.md` — `AzureWebJobsStorage` environment variable; `CONTRIBUTING.md` local NuGet path uses upper casing).
- Secrets must not be hardcoded in code or committed to the repository.
- Use explicit types rather than `var` to help readers understand the types involved.
- Keep sample code simple and well-commented, explaining the purpose of each meaningful step.

---

## Code Review Guidelines

When reviewing code, follow these guidelines:

- **Single review pass:** Provide all review comments in a single consolidated review pass. Do not scatter feedback across multiple partial reviews.
- **No stale comments:** Do not re-post or re-raise comments that have already been addressed by a subsequent commit. Only surface issues that still apply after the latest changes.
- **Respect justifications:** If a contributor has directly responded to a comment explaining why the code is written as it is, do not re-post that comment. Respect the explanation and move on.

---

## Change Flow

Follow this workflow when contributing (evidence: `CONTRIBUTING.md`):

1. Fork the repo and create a branch off `dev`.
2. Make your change and ensure all tests pass — including tests that appear unrelated.
3. Push to your fork and open a PR targeting the `dev` branch.
4. Address all review feedback.
5. Rebase your commits into meaningful units before merge (`git rebase -i HEAD~N`).
