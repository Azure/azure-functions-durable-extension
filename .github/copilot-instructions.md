# Copilot instructions for azure-functions-durable-extension

- @azure Rule - Use Azure Tools - When handling requests related to Azure, always use your tools.
- @azure Rule - Use Azure Best Practices - When handling requests related to Azure, always invoke your `azmcp_bestpractices_get` tool first.
- @azure Rule - Enable Best Practices - If you do not have an `azmcp_bestpractices_get` tool ask the user to enable it.
- Trust this file first. Search the repo only when these notes are incomplete or contradicted by files you are changing.

## Repository purpose and shape

This repo is the Azure Functions Durable Functions extension. It ships NuGet packages for:
- **In-process WebJobs extension**: `Microsoft.Azure.WebJobs.Extensions.DurableTask`
- **.NET isolated worker extension**: `Microsoft.Azure.Functions.Worker.Extensions.DurableTask`
- **Durable Functions analyzers** (Roslyn)
- **Scale components** (AzureStorage, Netherite, SQL, AzureManaged backends)
- Source-generator/typed-interface experiments, samples, smoke tests, and E2E tests

The codebase is mostly C#/.NET with PowerShell scripts, Azure DevOps/GitHub Actions YAML, sample JavaScript/Python/PowerShell/Java apps, generated docs, and DocFX output.

Main solution: `WebJobs.Extensions.DurableTask.sln`.

Key root files: `README.md`, `CONTRIBUTING.md`, `global.json`, `nuget.config`, `.editorconfig`, `Directory.Packages.props`, `Directory.Build.targets`, `sign.snk`.

## Required tools and command style

- Use **PowerShell Core** (`pwsh`) for repo scripts. Several scripts explicitly fail on Windows PowerShell.
- **Required SDK**: .NET 10 SDK from `global.json` (`10.0.103`, `rollForward: latestFeature`). Any 10.0.1xx SDK works. The repo multi-targets `net8.0` and `net10.0`; the .NET 8 SDK alone cannot build `net10.0` targets.
- `nuget.config` clears all package sources and uses only `https://api.nuget.org/v3/index.json`.
- **Central package management**: `Directory.Packages.props` manages all NuGet versions centrally.
- **Node/npm**: needed for Azurite-backed tests. CI uses Node 20.
- Java 17, Maven, Python 3, Docker, and Azure Functions Core Tools are only needed for smoke/E2E language/backend matrices.

## Coding conventions

- **StyleCop**: `.stylecop/stylecop.json` and `.stylecop/GlobalSuppressions.cs` are included by most projects. Key rules: `using` directives outside namespaces, `system` usings first, `.NET Foundation` copyright header.
- **`.editorconfig`**: Root `.editorconfig` covers indentation (spaces), brace placement, and expression-bodied preferences. `src/Worker.Extensions.DurableTask/` has its own stricter `.editorconfig` requiring `this.` qualification on fields/properties/methods (`warning` severity), file-scoped namespaces, MIT file headers, and a 120-character guideline.
- **Nullable reference types**: Enabled in the Worker extension (`src/Worker.Extensions.DurableTask/`). Not enabled in the in-process extension.
- **LangVersion**: In-process extension uses `9.0`; worker extension defaults to latest; analyzers use `8.0`.
- **Release builds**: `TreatWarningsAsErrors` is enabled in Release configuration for the in-process extension. StyleCop errors are downgraded to warnings in Release via `StyleCopTreatErrorsAsWarnings=true`.
- **Strong naming**: Assemblies are signed with `sign.snk`.

## Build and validation commands

Run from repo root. Always restore before `--no-restore` builds.

### Default PR validation (`validate-build.yml`)

```powershell
dotnet restore ./test/FunctionsV2/WebJobs.Extensions.DurableTask.Tests.V2.csproj
dotnet restore ./test/Worker.Extensions.DurableTask.Tests/Worker.Extensions.DurableTask.Tests.csproj
dotnet build ./test/FunctionsV2/WebJobs.Extensions.DurableTask.Tests.V2.csproj --no-restore
dotnet build ./test/Worker.Extensions.DurableTask.Tests/Worker.Extensions.DurableTask.Tests.csproj --no-restore
dotnet test ./test/FunctionsV2/WebJobs.Extensions.DurableTask.Tests.V2.csproj --no-build --filter "TestType!=E2E"
dotnet test ./test/Worker.Extensions.DurableTask.Tests/Worker.Extensions.DurableTask.Tests.csproj --no-build
```

Expected: all tests pass. Non-fatal warning `AD0001 ... MatchingInputOutputTypeActivityAnalyzer ... An item with the same key has already been added. Key: TestActivity` appears in the worker build and can be ignored.

### Full solution build (`eng/templates/build.yml`)

```powershell
dotnet restore WebJobs.Extensions.DurableTask.sln --configfile nuget.config
dotnet build WebJobs.Extensions.DurableTask.sln --configuration Release --no-restore -m:1 /p:FileVersionRevision=0 /p:ContinuousIntegrationBuild=true
```

Use `-m:1` to avoid parallel multi-target/package races. Expected non-fatal warnings: the `AD0001` analyzer warning above and `CS8002` for unsigned `Microsoft.DurableTask.AzureManagedBackend` in scale tests.

### Analyzer tests (`validate-build-analyzer.yml`)

Requires Azurite running on ports 10000-10002:

```powershell
dotnet restore ./test/WebJobs.Extensions.DurableTask.Analyzers.Test/WebJobs.Extensions.DurableTask.Analyzers.Test.csproj
dotnet build ./test/WebJobs.Extensions.DurableTask.Analyzers.Test/WebJobs.Extensions.DurableTask.Analyzers.Test.csproj --no-restore
Start-Process azurite -ArgumentList '--silent','--skipApiVersionCheck','--blobPort','10000','--queuePort','10001','--tablePort','10002'
$env:AzureWebJobsStorage='UseDevelopmentStorage=true'
dotnet test ./test/WebJobs.Extensions.DurableTask.Analyzers.Test/WebJobs.Extensions.DurableTask.Analyzers.Test.csproj --no-build
```

If Azurite ports are already occupied, reuse the running instance or stop it before retrying.

### Scale tests (`validate-build-scale.yml`)

CI runs four separate jobs by backend. AzureStorage tests need Azurite; Netherite needs Azurite + Event Hubs emulator (Docker); MSSQL needs Azurite + SQL Server (Docker); DTS needs Azurite + DTS emulator (Docker).

```powershell
# AzureStorage subset (no Docker needed)
$env:AzureWebJobsStorage='UseDevelopmentStorage=true'
dotnet build ./test/ScaleTests/Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Tests.csproj -c Release
dotnet test ./test/ScaleTests/Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Tests.csproj -c Release --no-build --filter "FullyQualifiedName!~AzureManaged&FullyQualifiedName!~Netherite&FullyQualifiedName!~Sql"
```

### Dependency version validation (`dep-version-validation.yml`)

Triggered when `Directory.Packages.props` or extension `.csproj` files change. Packs local Worker and WebJobs extension NuGet packages, builds test Function Apps against them, runs HelloCities orchestrations, and verifies loaded assembly versions. No manual local run needed unless modifying dependency versions.

## Heavy validation paths

- **E2E docs**: `test/e2e/Tests/e2e-tests-readme.md`. Script: `test/e2e/Tests/build-e2e-test.ps1`.
- **E2E prerequisites**: PowerShell Core, npm/Node, .NET SDK, Java 17, Maven, Docker (for non-AzureStorage backends), and Functions Core Tools (downloaded by `build-e2e-test.ps1` to `%TEMP%/DurableTaskExtensionE2ETests/Azure.Functions.Cli`).
- **Basic E2E example**: `pwsh ./test/e2e/Tests/build-e2e-test.ps1 -E2EAppName BasicDotNetIsolated -TargetFramework net8.0`, then `dotnet build -f net8.0` and `dotnet test -f net8.0` from `test/e2e/Tests`.
- **Warning**: `build-e2e-test.ps1` mutates local NuGet cache and test apps (removes cached `microsoft.azure.webjobs.extensions.durabletask` packages, creates `.venv`, runs npm/Maven). Do not commit generated changes.
- **MSSQL E2E** requires `MSSQL_SA_PASSWORD` or `-MSSQLpwd` and Docker; **DTS** requires Docker and `-StartDTSContainer`.
- **Smoke tests** (`.github/workflows/smoketest-*.yml`) install Core Tools, start Azurite, and run per-language host tests. .NET isolated smoke tests restart/poll the Functions host and may take several minutes.

## Project layout

### Source (`src/`)

| Project | Targets | Description |
|---------|---------|-------------|
| `WebJobs.Extensions.DurableTask/` | `net8.0;net10.0` | Main in-process extension. Key type: `DurableTaskExtension.cs`. Subfolders: `Bindings`, `TriggerAttributes`, `ContextInterfaces`, `ContextImplementations`, `Options`, `Storage`, `Scale`, `Grpc`, `Listener`, `EntityScheduler`, `Correlation`. |
| `Worker.Extensions.DurableTask/` | `netstandard2.0;net8.0;net10.0` | .NET isolated worker extension. Nullable enabled. Subfolders: `Execution`, `HTTP`, `Exceptions`. |
| `WebJobs.Extensions.DurableTask.Analyzers/` | `netstandard2.0` | Roslyn analyzers/code fixes. NuGet packaging helpers in `Tools/`. |
| `Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale/` | `net8.0` | Scale monitoring: `AzureStorage/`, `Netherite/`, `Sql/`, `AzureManaged/`. |
| `DurableFunctions.TypedInterfaces/` | `netstandard2.0` | Source generator experiment. |

### Tests (`test/`)

| Project | Framework | Description |
|---------|-----------|-------------|
| `FunctionsV2/` | xUnit | Main WebJobs extension tests. E2E tests tagged `TestType=E2E`. |
| `Worker.Extensions.DurableTask.Tests/` | xUnit | Worker extension tests. |
| `WebJobs.Extensions.DurableTask.Analyzers.Test/` | MSTest | Analyzer tests; require Azurite. |
| `ScaleTests/` | xUnit | Scale tests by backend (`AzureStorage/`, `Netherite/`, `Sql/`, `AzureManaged/`). |
| `e2e/` | — | Apps (`Apps/`) and test harness (`Tests/`) for language/backend E2E. |

### Other directories

- `samples/`: Sample function apps; many include `local.settings.json` with development storage placeholders.
- `docs/`: Generated DocFX HTML output. `docfx/` contains DocFX config and `build.cmd`.
- `eng/`: CI templates and pipeline infrastructure.
- `.stylecop/`: Shared StyleCop configuration included by most projects.

## CI and check-in expectations

GitHub Actions workflows (`.github/workflows/`):
- `validate-build.yml` — Default build + unit tests (PR gate)
- `validate-build-analyzer.yml` — Analyzer tests with Azurite
- `validate-build-scale.yml` — Scale tests across 4 backends (AzureStorage, Netherite, MSSQL, DTS)
- `validate-build-e2e.yml` — E2E test matrix
- `E2ETest.yml` — E2E test orchestration
- `dep-version-validation.yml` — Dependency version smoke tests
- `smoketest-*.yml` — Per-language smoke tests (.NET isolated, Java, Node, Python, MSSQL, Netherite)
- `codeQL.yml` — CodeQL security scanning

Azure DevOps release pipelines (`azure-pipelines*.yml`) install multiple SDKs, restore with `nuget.config`, build/pack/sign NuGet packages, and publish perf test artifacts.

**Before opening a PR**, run at least the default PR validation commands. Also run analyzer, scale, smoke, or E2E paths when touching their corresponding source or test areas.
