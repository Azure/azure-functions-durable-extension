# End-to-End Test Project

This document provides instructions on how to use the end-to-end (E2E) test project for the Azure Functions Durable Extension.

## Prerequisites

- [PowerShell](https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-7.5)
- [npm/Node](https://nodejs.org/en/download)
- [.NET SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- [Java 17](https://www.oracle.com/java/technologies/javase/jdk17-archive-downloads.html)
- [Maven](https://maven.apache.org/download.cgi?.)
- [Docker](https://www.docker.com/products/docker-desktop/)

Note: Docker is only required if running against a non-AzureStorage backend

## About the tests

The goal of this project is to run a set of standardized behavior checks against real FunctionApps using Core Tools to validate key scenarios and guard against breaking changes. This is accomplished by defining one app per Durable Functions language in the /test/e2e/apps folder, which defines a set of standardized functions. The pipeline for these tests will run against each app in sequence using a variety of supported Durable storage backends (Azure Storage, MSSQL, DTS).

## Running the E2E Tests

### Step 1: Increment the host and worker package versions (optional)

Note: This step is optional. However, if you do not perform this step, the versions of these two packages in your local NuGet cache will be replaced with the build output from the test run, which may lead to unexpected behavior debugging live versions in other apps. Be warned.

Modify the following files:

```text
\src\WebJobs.Extensions.DurableTask\WebJobs.Extensions.DurableTask.csproj
\src\Worker.Extensions.DurableTask\AssemblyInfo.cs
\src\Worker.Extensions.DurableTask\Worker.Extensions.DurableTask.csproj
```

### Step 2: Configure the test project for the language/backend you are interested in

With no other changes, the test project will run against dotnet-isolated with the Azure Storage backend. If you need to run the tests against a different language or backend, you can use test.runsettings to accomplish this when debugging from Visual Studio. Edit the E2ETests.csproj file and uncomment the RunSettingsFilePath line. Next, go to the test.runsettings file found at the root of the test project and make modifications as necessary.

NOTE: If running through the commandline using `dotnet test`, you will need to define the environment variables from test.runsettings manually.

### Step 3: Build the E2E Test Project

To build the E2E test project, run the following PowerShell script:

```powershell
./build-e2e-test.ps1
```

This script prepares your system for running the E2E tests by performing the following steps:

1. Installing a copy of Core Tools into your system's temp directory to ensure an unmodified Core Tools. This is necessary, as the tests will not attempt to use the "func" referenced in PATH
2. Ensure the test app(s) are running the correct extension code by:
    - Building the host and worker extensions from their projects within this repo
    - Packing the extensions into local NuGet packages
    - Copying the built packages into the test app's local nuget source folder as configured in nuget.config
    - Updating the test app's .csproj files to reference the local package version
    - Building the test app projects
3. Install and start azurite emulator using npm

NOTE: It should not be necessary to run start-emulators.ps1 manually, as it should be called by the build script. If you have a instance of Azurite already running, it will automatically skip this step.

NOTE: The build script will make changes to the extensions.csproj file in all non-dotnet language apps. Do not commit these changes.

#### Other backends

build-e2e-test.ps1 includes several flags to assist setup for other storage backends.

For MSSQL, you can use `-StartMSSqlContainer` to spin up a MSSQL docker container. Define the sa password you will use either in the MSSQL_SA_PASSWORD environment variable or with the `-MSSQLpwd` script argument.

For DTS, pass `-StartDTSContainer` flag. This will create a docker container using the DTS emulator image.

### Step 4: Build the test project

At this point, you are ready to run the tests. You may start them using the Visual Studio test explorer as normal, the tests will take care of instancing Core Tools and starting the apps.
NOTE: ENSURE AZURITE IS RUNNING. If Azure is not available, the function app loaded by the test framework will 502 and the test suite will loop indefinitely waiting for it to come up. This will be addressed in future versions.

### Step 5: Attach a Debugger

To debug the extension code while running test functions, you can attach a debugger directly to Core Tools from whichever repository you are interested in:

- For debugging the WebJobs extension in this repo, attach to `func.exe`
- For debugging Durabletask.Core or DurableTask.AzureStorage, attach to `func.exe`
- For debugging the Worker extension from this repo for dotnet-isolated, or the functionapp code, attach to `dotnet.exe` that is a child process of `func.exe`

Set a breakpoint on the first line of the test so that the test will start the Core Tools process before ataching.

## Understanding the test code

### Exclusions

Some tests are incompatible with a certain Durable storage backend, a certain functions language, or a combination of both of these factors. These tests can be skipped using test traits:

```csharp
    [Trait("MSSQL", "Skip")] // Skips the test for all languages when running with MSSQL storage backend
```

```csharp
    [Trait("PowerShell", "Skip")] // Skips the test for all storage backends when language is PowerShell
```

```csharp
    [Trait("PowerShell-MSSQL", "Skip")] // Skips the test only when running with both powershell and MSSQL
```

Supported values are `AzureStorage`, `MSSQL` and `DTS` for storage backends, and `Dotnet`, `PowerShell`, `Python`, `Node`, and `Java` for languages. When skipping a specific combo, the order is "\[language]-\[backend]". Multiple tags may be used to skip multiple scenarios.

IMPORTANT: Include a comment after each skip tag explaining why it is skipped. If it is due to a bug, link the GitHub issue in the comment (and ideally, note which test is failing on the GitHub issue as well)

### Language-specific behavior

Every test file will have a `FunctionAppFixture` available. This fixture allows interaction with the functions host from the test perspective, including Core Tools output logs which can be used when verifying test outputs, and an ITestLanguageLocalizer. ITestLanguageLocalizer provides two ways of defining language-specific behavior. The convention for this is as follows:

If the behavior is a difference in output formatting, and we don't expect to ever try to get parity between the languages, use GetLocalizedStringValue(). You will need to define a key-value pair in each language's ITestLanguageLocalizer implementation for your string, and then you can call GetLocalizedStringValue(key) in your test. This keeps test code clean and readable.

If the behavior is with logic or is something that we need to eventually address in the language worker to improve parity, use GetLanguageType() and if/case statements in the test code instead. This will improve visibility of these kinds of inconsistencies and (hopefully) motivate eventual changes towards parity.

### FAQ

#### Exception: GRPC max size exceeded

If you see the following exception while running the tests (specifically, DurableTaskClientWriteOutputTests and ListAllOrchestrations_ShouldSucceed):

```text
Exception: Grpc.Core.RpcException: Status(StatusCode="ResourceExhausted", Detail="Received message exceeds the maximum configured message size."
```

This is due to your TaskHub history being too large to be processed by the app. This can happen in (at least) dotnet-isolated and Java.
To resolve this, there are several steps, in order of increasing severity:

1. Run the test app manually and call PurgeOrchestrationHistory to delete all past instances.
2. Use the Azure Storage explorer to connect to your azurite instance and delete the Task Hub manually by deleting the storage queues and tables.
3. Completely remove the Azurite state and start fresh. This can be done by stopping the azurite process, navigating to the directory where it is running (for the azurite instance started by build-e2e-test.ps1 on Windows this is %LOCALAPPDATA%/Temp/DurableTaskExtensionE2ETests/azurite) and deleting all Azurite files.

#### Non-dotnet languages

See step #2 in `Running the E2E Tests` for instructions on running the tests against non-dotnet languages. When editing test.runsettings, the following values are accepted:
**`E2E_TEST_FUNCTIONS_LANGUAGE`** — Supported values: `dotnet-isolated`, `powershell`, `python`, `node`, `java`
**`TEST_APP_NAME`** — Supported values: `BasicDotNetIsolated`, `BasicPowerShell`, `BasicPython`, `BasicNode`, `BasicJava`

It is important that the language and app name are compatible.

#### Non-`Azure Storage` backends

See step #3 in `Running the E2E Tests` for instructions on running the tests against non-`Azure Storage` backends. When editing test.runsettings, the following values are accepted for `E2E_TEST_DURABLE_BACKEND`: `AzureStorage`, `AzureManaged`, `MSSQL`.

Note: When using MSSQL, it is also necessary to provide the `MSSQL_SA_PASSWORD` environment variable. This can be set in test.runsettings but must also be available to ./build-e2e-test.ps1 when using the -$StartMSSqlContainer flag, either using the environment variable with the same name or passing the password using -MSSQLpwd to the script arguments.

#### build-e2e-test.ps1 helper flags

The build-e2e-test.ps1 script has several flags and parameters that may be helpful for speeding up local development.

- `-StartMSSqlContainer` and `-StartDTSContainer` will automatically pull the docker images for MSSQL or the Durable Task Scheduler, respectively, and start them up using arguments compatible with the E2E tests. You should also use `-MSSQLpwd` to provide the SA password that will be used for the MSSQL instance.
- `-SkipBuild` will skip building the extension and tests apps entirely, useful when you want to download Core Tools or start an emulator without waiting for these components to build.
- `-E2EAppName` may be used to build only one of the test apps in the project, for example passing `-E2EAppName BasicJava` will only build BasicJava, saving time during build.
- `-SkipCoreTools` and `-SkipStorageEmulator` work as described, but build-e2e-test.ps1 will automatically skip these steps if Core Tools or the Storage emulator are already present/running, so these flags are useful only in niche situations.
