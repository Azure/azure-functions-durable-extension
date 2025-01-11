# End-to-End Test Project

This document provides instructions on how to use the end-to-end (E2E) test project for the Azure Functions Durable Extension.

## Prerequisites

- PowerShell
- npm/Node
- .NET SDK

## Running the E2E Tests

### Step 1: Increment the host and worker package versions (optional)

Note: This step is optional. However, if you do not perform this step, the versions of these two packages in your local NuGet cache will be replaced with the build output from the test run, which may lead to unexpected behavior debugging live versions in other apps. Be warned. 

Modify the following files:
```
\src\WebJobs.Extensions.DurableTask\WebJobs.Extensions.DurableTask.csproj
\src\Worker.Extensions.DurableTask\AssemblyInfo.cs
\src\Worker.Extensions.DurableTask\Worker.Extensions.DurableTask.csproj
```

### Step 2: Build the E2E Test Project

To build the E2E test project, run the following PowerShell script:

```powershell
./build-e2e-test.ps1
```

This script prepares your system for running the E2E tests by performing the following steps:
1. Installing a copy of Core Tools into your system's temp directory to ensure an unmodified Core Tools. This is necessary, as the tests will not attempt to use the "func" referenced in PATH 
2. Ensure the test app(s) are running the correct extension code by: 
    * Building the host and worker extensions from their projects within this repo
    * Packing the extensions into local NuGet packages
    * Copying the built packages into the test app's local nuget source folder as configured in nuget.config
    * Updating the test app's .csproj files to reference the local package version
    * Building the test app projects
3. Install and start azurite emulator using Node

NOTE: It should not be necessary to run start-emulators.ps1 manually, as it should be called by the build script. If you have a instance of Azurite already running, it will recognize and skip this step. 

### Step 3: Build the test project

At this point, you are ready to run the tests. You may start them using the Visual Studio test explorer as normal, the tests will take care of instancing Core Tools and starting the apps. 
NOTE: ENSURE AZURITE IS RUNNING. If Azure is not available, the function app loaded by the test framework will 502 and the test suite will loop indefinitely waiting for it to come up. This will be addressed in future versions. 

### Step 4: Attach a Debugger

To debug the extension code while running test functions, you need to attach a debugger to the `func` process before the test code runs. Follow these steps:

1. Open your preferred IDE (e.g., Visual Studio or Visual Studio Code).
2. Set a breakpoint in the test.
3. Manually search for and attach the test process. For Out-Of-Process workers, attach func.exe to debug the host extension, and attach the child process representing the worker (dotnet.exe for dotnet OOProc) to debug the worker extension. 

## Conclusion

Following these steps will help you set up and run the E2E tests for the Azure Functions Durable Extension project. If you encounter any issues, refer to the project documentation or seek help from the community.
