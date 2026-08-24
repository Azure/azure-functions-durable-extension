#!/usr/bin/env pwsh
#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

[CmdletBinding()]
param(
    [Switch]
    $SkipStorageEmulator,

    [Switch]
    $StartMSSqlContainer,

    [string]
    $MSSQLpwd,

    [Switch]
    $StartDTSContainer,

    # Skip downloading Core Tools (assumes they are already installed in the temp directory).
    # This does NOT prevent Core Tools from being added to PATH if the temp directory exists.
    [Switch]
    $SkipCoreTools,

    # Force re-download of Core Tools even if they already exist on disk. Ignored when -SkipCoreTools is set.
    [Switch]
    $UpdateCoreTools,

    # This param can be used during local runs of the build script to deliberately skip the build and run only the azurite/mssql logic
    # For instance, the command ./build-e2e-test.ps1 -SkipBuild -StartMSSqlContainer will start azurite and the MSSQL docker container only. 
    [Switch]
    $SkipBuild,

    # Target a specific test app to build. Ignored if -SkipBuild is set. If not specified, all test apps will be built.
    [string]
    $E2EAppName = "",

    # Target framework to build (e.g., net8.0, net10.0). If not specified, builds all TFMs.
    [string]
    $TargetFramework = ""
)

if ($PSVersionTable.PSEdition -ne 'Core') {
    Write-Warning "You are not running PowerShell Core. Please switch to PowerShell Core (>= PS 6) for better compatibility and performance."
    Write-Warning "See https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-7.5"
    exit 1
}

$ErrorActionPreference = "Stop"

. "$PSScriptRoot\resolve-core-tools-path.ps1"

$ProjectBaseDirectory = "$PSScriptRoot\..\..\..\"
$ProjectTemporaryPath = Join-Path ([System.IO.Path]::GetTempPath()) "DurableTaskExtensionE2ETests"
New-Item -Path $ProjectTemporaryPath -ItemType Directory -ErrorAction SilentlyContinue
$WebJobsExtensionProjectDirectory = Join-Path $ProjectBaseDirectory "src\WebJobs.Extensions.DurableTask"
$E2EAppParentDirectory = Join-Path $ProjectBaseDirectory "test\e2e\Apps"
$CfsNpmConfigPath = (Resolve-Path (Join-Path $ProjectBaseDirectory "eng\cfs\.npmrc")).Path

if (!$env:NPM_CONFIG_USERCONFIG) {
  $env:NPM_CONFIG_USERCONFIG = $CfsNpmConfigPath
}

if (!$env:PIP_INDEX_URL) {
  $env:PIP_INDEX_URL = "https://pkgs.dev.azure.com/azfunc/public/_packaging/upstream-public/pypi/simple/"
}

$LocalNugetCacheDirectory = $env:NUGET_PACKAGES
if (!$LocalNugetCacheDirectory) {
  $LocalNugetCacheDirectory = "$env:USERPROFILE\.nuget\packages"
}

$FunctionsRuntimeVersion = 4

# A function that checks exit codes and fails script if an error is found 
function StopOnFailedExecution {
  if ($LastExitCode) 
  { 
    exit $LastExitCode 
  }
}

if ($SkipCoreTools)
{
  Write-Host "---Skipping Core Tools download (-SkipCoreTools)---"
}
elseif ((Test-Path $FUNC_CLI_DIRECTORY) -and -not $UpdateCoreTools)
{
  Write-Host "---Skipping Core Tools download (already exists; use -UpdateCoreTools to force)---"
}
else
{
  $arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
  if ($IsWindows) {
      $os = "win"
      $coreToolsURL = $env:CORE_TOOLS_URL
  }
  else {
      if ($IsMacOS) {
          $os = "osx"
      } else {
          $os = "linux"
          $coreToolsURL = $env:CORE_TOOLS_URL_LINUX
      }
  }

  if ([string]::IsNullOrWhiteSpace($coreToolsURL))
  {
    $coreToolsURL = "https://github.com/Azure/azure-functions-core-tools/releases/download/$CORE_TOOLS_VERSION/Azure.Functions.Cli.$os-$arch.$CORE_TOOLS_VERSION.zip"
  }

  Write-Host ""
  Write-Host "---Downloading the Core Tools for Functions V$FunctionsRuntimeVersion---"
  Write-Host "Core Tools download url: $coreToolsURL"

  Write-Host 'Deleting Functions Core Tools if exists...'
  Remove-Item -Force "$FUNC_CLI_DIRECTORY.zip" -ErrorAction Ignore
  Remove-Item -Recurse -Force $FUNC_CLI_DIRECTORY -ErrorAction Ignore

  $output = "$FUNC_CLI_DIRECTORY.zip"
  Invoke-RestMethod -Uri $coreToolsURL -OutFile $output

  Write-Host 'Extracting Functions Core Tools...'
  Expand-Archive $output -DestinationPath $FUNC_CLI_DIRECTORY

  Write-Host 'Cleaning up downloaded zip...'
  Remove-Item -Force $output -ErrorAction SilentlyContinue

  if ($IsMacOS -or $IsLinux)
  {
    & "chmod" "a+x" "$FUNC_CLI_DIRECTORY/func"
  }
  
  Write-Host "------"
}

# Ensure Core Tools are on PATH regardless of whether the download was skipped.
# -SkipCoreTools only skips the download; if the directory exists, we still need it on PATH.
if (Test-Path $FUNC_CLI_DIRECTORY) {
  Write-Host "Adding Functions Core Tools to PATH..."
  if ($IsWindows) {
      $env:PATH = $env:PATH + ";$FUNC_CLI_DIRECTORY"
  } else {
      $env:PATH = $env:PATH + ":$FUNC_CLI_DIRECTORY"
  }
}

function InstallExtensionAndBuildTestApp($testAppDir) {
    Write-Host "Building test app $testAppDir"
    Push-Location $testAppDir
    try {

    Write-Host "Removing cached WebJobs extension versions from nuget cache, if exists"
    $cachedVersionFolders = Get-ChildItem -Path (Join-Path $LocalNugetCacheDirectory "microsoft.azure.webjobs.extensions.durabletask") -Directory -ErrorAction SilentlyContinue
    $cachedVersionFolders | ForEach-Object {
      Write-Host "Removing cached version $($_.Name) from nuget cache"
      Remove-Item -Recurse -Force $_.FullName -ErrorAction Stop
    }

    if (!(Test-Path ".\app.csproj")) {
      Write-Host "Syncing extensions"
      if ((Test-Path (Join-Path $FUNC_CLI_DIRECTORY "func")) -or (Test-Path (Join-Path $FUNC_CLI_DIRECTORY "func.exe"))) {
        .(Join-Path $FUNC_CLI_DIRECTORY "func") extensions sync
        StopOnFailedExecution
      }
      else {
        Write-Warning "func command not found. Skipping extensions sync."
      }
    }

    if (Test-Path ".\requirements.txt") {
      Write-Host "Creating Python virtual environment in $(Join-Path $testAppDir '.venv')"
      python -m venv .venv
      StopOnFailedExecution

      if ($IsWindows) {
        .  .\.venv\Scripts\Activate.ps1
      } else {
        .  ./.venv/bin/Activate.ps1
      }

      python -m pip install --upgrade --requirement "requirements.txt"
      StopOnFailedExecution

      deactivate
    }

    if (Test-Path ".\package-lock.json") {
      Write-Host "Installing npm packages"
      npm install
      StopOnFailedExecution
      npm run clean
      StopOnFailedExecution
      npm run build
      StopOnFailedExecution
    }

    if (Test-Path ".\pom.xml") {
      Write-Host "Building Java project"
      mvn clean package -q
      StopOnFailedExecution
    }
    
    if (Test-Path ".\app.csproj") {
      Write-Host "Building app project"
      if ($TargetFramework) {
        dotnet clean app.csproj -f $TargetFramework
        StopOnFailedExecution
        dotnet build app.csproj -f $TargetFramework
      } else {
        dotnet clean app.csproj
        StopOnFailedExecution
        dotnet build app.csproj
      }
      StopOnFailedExecution
    }

    } finally {
      Pop-Location
    }
}

if (!$SkipBuild)
{
  Write-Host "Building WebJobs extension project"
  
  # Do NOT use --output with multi-targeted projects to avoid race conditions
  # when multiple TFMs try to write to the same output directory (MSB4018).
  # Disable GeneratePackageOnBuild to prevent parallel TFM builds from racing
  # to write the same .nuspec file. The E2E test apps produce their own local
  # .nupkg via MSBuild PreBuild targets, so the package is not needed here.

  dotnet build -c Debug /p:GeneratePackageOnBuild=false "$WebJobsExtensionProjectDirectory\WebJobs.Extensions.DurableTask.csproj"

  if ($LASTEXITCODE -ne 0) { Set-Location $PSScriptRoot; throw "WebJobs Extension build failed" }

  if ($E2EAppName)
  {
    InstallExtensionAndBuildTestApp (Join-Path $E2EAppParentDirectory $E2EAppName)
  }
  else {
    Get-ChildItem -Path $E2EAppParentDirectory -Directory | ForEach-Object {
      $E2EAppProjectDirectory = $_.FullName

      InstallExtensionAndBuildTestApp $E2EAppProjectDirectory
    }
  }

  if ($LASTEXITCODE -ne 0) { Set-Location $PSScriptRoot; throw "Test app build failed." }
}

Set-Location $PSScriptRoot

if ($SkipStorageEmulator)
{
  Write-Host
  Write-Host "---Skipping emulator startup---"
  Write-Host
}
else 
{
  .\start-emulators.ps1 -SkipStorageEmulator:$SkipStorageEmulator -EmulatorStartDir $ProjectTemporaryPath
}

function StartMSSQLContainer($mssqlPwd) {
  Write-Host "Pulling down the mcr.microsoft.com/mssql/server:2022-latest image..."
  docker pull mcr.microsoft.com/mssql/server:2022-latest

  # Start the SQL Server docker container with the specified edition
  Write-Host "Starting SQL Server 2022-latest Express docker container on port 1433" -ForegroundColor DarkYellow
  docker run --name mssql-server -e ACCEPT_EULA=Y -e "MSSQL_SA_PASSWORD=$mssqlPwd" -e "MSSQL_PID=Express" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest

  if ($LASTEXITCODE -ne 0) {
      exit $LASTEXITCODE
  }

  # Wait for SQL Server to be ready by polling with sqlcmd
  Write-Host "Waiting for SQL Server to become ready..." -ForegroundColor Yellow
  $maxAttempts = 30
  for ($i = 1; $i -le $maxAttempts; $i++) {
      $result = docker exec mssql-server /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$mssqlPwd" -Q "SELECT 1" -C -b 2>&1
      if ($LASTEXITCODE -eq 0) {
          Write-Host "SQL Server is ready after $i seconds." -ForegroundColor Green
          break
      }
      if ($i -eq $maxAttempts) {
          Write-Error "SQL Server did not become ready within $maxAttempts seconds."
          docker logs mssql-server 2>&1 | Select-Object -Last 20
          exit 1
      }
      Start-Sleep -Seconds 1
  }

  # Check to see what containers are running
  docker ps
}

function StartDTSContainer() {
  Write-Host "Pulling down the mcr.microsoft.com/dts/dts-emulator:latest image..."
  docker pull mcr.microsoft.com/dts/dts-emulator:latest

  # Start the DTS Server docker container with the specified edition
  Write-Host "Starting DTS docker container on port 8080" -ForegroundColor DarkYellow
  docker run -i --name dts-emulator --rm -p 8080:8080 -p 8081:8081 -p 8082:8082 -d mcr.microsoft.com/dts/dts-emulator:latest

  if ($LASTEXITCODE -ne 0) {
      exit $LASTEXITCODE
  }

  # Poll until the emulator port is accepting TCP connections instead of a fixed sleep
  Write-Host "Waiting for DTS emulator to become ready..." -ForegroundColor Yellow
  $maxAttempts = 60
  for ($i = 1; $i -le $maxAttempts; $i++) {
      try {
          $tcp = New-Object System.Net.Sockets.TcpClient
          try {
              $tcp.Connect("localhost", 8080)
              Write-Host "DTS emulator is ready after $i seconds." -ForegroundColor Green
              break
          } finally {
              $tcp.Dispose()
          }
      } catch { }
      if ($i -eq $maxAttempts) {
          Write-Error "DTS emulator did not become ready within $maxAttempts seconds."
          docker logs dts-emulator 2>&1 | Select-Object -Last 20
          exit 1
      }
      Start-Sleep -Seconds 1
  }

  # Check to see what containers are running
  docker ps
}

Set-Location $PSScriptRoot

if ($StartMSSqlContainer)
{
  if (!$MSSQLpwd) {
    $MSSQLpwd = $env:MSSQL_SA_PASSWORD
    if (!$MSSQLpwd) {
      Write-Warning "No MSSQL_SA_PASSWORD environment variable found! Skipping SQL Server container startup."
    }
  }
  if ($MSSQLpwd) {
    StartMSSQLContainer $MSSQLpwd
  }
}

if ($StartDTSContainer)
{
    StartDTSContainer
}

StopOnFailedExecution
