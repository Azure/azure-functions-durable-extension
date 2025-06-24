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

    [Switch]
    $StartDTSContainer,

    [Switch]
    $SkipCoreTools,

    # This param can be used during local runs of the build script to deliberately skip the build and run only the azurite/mssql logic
    # For instance, the command ./build-e2e-test.ps1 -SkipBuild -StartMSSqlContainer will start azurite and the MSSQL docker container only. 
    [Switch]
    $SkipBuild,

    [string]
    $E2EAppName = ""
)

if ($PSVersionTable.PSEdition -ne 'Core') {
    Write-Warning "You are not running PowerShell Core. Please switch to PowerShell Core (>= PS 6) for better compatibility and performance."
    Write-Warning "See https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-7.5"
    exit 1
}

$ErrorActionPreference = "Stop"

$CORE_TOOLS_VERSION = '4.0.7317'

$ProjectBaseDirectory = "$PSScriptRoot\..\..\..\"
$ProjectTemporaryPath = Join-Path ([System.IO.Path]::GetTempPath()) "DurableTaskExtensionE2ETests"
New-Item -Path $ProjectTemporaryPath -ItemType Directory -ErrorAction SilentlyContinue
$WebJobsExtensionProjectDirectory = Join-Path $ProjectBaseDirectory "src\WebJobs.Extensions.DurableTask"
$E2EAppParentDirectory = Join-Path $ProjectBaseDirectory "test\e2e\Apps"

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

$FUNC_CLI_DIRECTORY = Join-Path $ProjectTemporaryPath 'Azure.Functions.Cli'
if($SkipCoreTool -or (Test-Path $FUNC_CLI_DIRECTORY))
{
  Write-Host "---Skipping Core Tools download---"  
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

  if ($IsMacOS -or $IsLinux)
  {
    & "chmod" "a+x" "$FUNC_CLI_DIRECTORY/func"
  }
  
  Write-Host "------"
}

function InstallExtensionAndBuildTestApp($testAppDir) {
    Write-Host "Removing old packages from test app $testAppDir"

    $AppPackageLocation = Join-Path $testAppDir 'packages'
    if (!(Test-Path $AppPackageLocation)) {
      New-Item -Path $AppPackageLocation -ItemType Directory -ErrorAction SilentlyContinue
    }
    $AppPackageLocation = Resolve-Path $AppPackageLocation
    Get-ChildItem -Path $AppPackageLocation -Include * -File -Recurse | ForEach-Object { $_.Delete()}
    
    Write-Host "Moving nupkg from WebJobs extension to $AppPackageLocation"
    Set-Location $BuildOutputLocation
    dotnet nuget push *.nupkg --source $AppPackageLocation
    
    Write-Host "Updating app .csproj to reference built package versions"
    Set-Location $testAppDir
    $files = Get-ChildItem -Path ./packages -Include * -File -Recurse
    $files | ForEach-Object {
      if ($_.Name -match 'Microsoft.Azure.WebJobs.Extensions.DurableTask')
      {
        $webJobsExtensionVersion = $_.Name -replace 'Microsoft.Azure.WebJobs.Extensions.DurableTask\.|\.nupkg'
    
        Write-Host "Removing cached version $webJobsExtensionVersion of WebJobs extension from nuget cache, if exists"
        $cachedVersionFolders = Get-ChildItem -Path (Join-Path $LocalNugetCacheDirectory "microsoft.azure.webjobs.extensions.durabletask") -Directory -ErrorAction Continue
        $cachedVersionFolders | ForEach-Object {
          if ($_.Name -eq $webJobsExtensionVersion)
          {
            Write-Host "Removing cached version $webJobsExtensionVersion from nuget cache"
            Remove-Item -Recurse -Force $_.FullName -ErrorAction Stop
          }
        }

        if (!(Test-Path ".\app.csproj")) {
          if (!(Test-Path ".\extensions.csproj")) {
            Write-Host "Creating extensions.csproj file"

            .(Join-Path $FUNC_CLI_DIRECTORY "func") extensions install --package Microsoft.Azure.Functions.Worker.Extensions.DurableTask --version $webJobsExtensionVersion

            # Fix for central package management being enabled in the project root
            $csprojContent = Get-Content -Path ".\extensions.csproj"
            $csprojContent = $csprojContent -replace '</TargetFramework>', "</TargetFramework>`n    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>"
            Set-Content -Path ".\extensions.csproj" -Value $csprojContent
          }

          Write-Host "Updating extensions.csproj to reference WebJobs extension version $webJobsExtensionVersion"
          
          dotnet add 'extensions.csproj' package 'Microsoft.Azure.WebJobs.Extensions.DurableTask' --version $webJobsExtensionVersion --source ".\packages" --no-restore
        }
      }
    }
    
    if (Test-Path ".\app.csproj") {
      Write-Host "Building app project"
      dotnet clean app.csproj
      dotnet build app.csproj
    }
}

if (!$SkipBuild)
{
  Write-Host "Building WebJobs extension project"
  
  $BuildOutputLocation = Join-Path $WebJobsExtensionProjectDirectory 'out'
  if (!(Test-Path $BuildOutputLocation)) {
    New-Item -Path $BuildOutputLocation -ItemType Directory -ErrorAction SilentlyContinue
  }
  $BuildOutputLocation = Resolve-Path $BuildOutputLocation
  Get-ChildItem -Path $BuildOutputLocation -Include * -File -Recurse | ForEach-Object { $_.Delete()}
  dotnet build -c Debug "$WebJobsExtensionProjectDirectory\WebJobs.Extensions.DurableTask.csproj" --output $BuildOutputLocation

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

  # The container needs a bit more time before it can start accepting commands
  Write-Host "Sleeping for 30 seconds to let the container finish initializing..." -ForegroundColor Yellow
  Start-Sleep -Seconds 30

  # Check to see what containers are running
  docker ps
}

function StartDTSContainer() {
  Write-Host "Pulling down the mcr.microsoft.com/dts/dts-emulator:v0.0.4 image..."
  docker pull mcr.microsoft.com/dts/dts-emulator:v0.0.4

  # Start the DTS Server docker container with the specified edition
  Write-Host "Starting DTS docker container on port 8080" -ForegroundColor DarkYellow
  docker run -i -p 8080:8080 -p 8082:8082 -d mcr.microsoft.com/dts/dts-emulator:v0.0.4

  if ($LASTEXITCODE -ne 0) {
      exit $LASTEXITCODE
  }

  # The container needs a bit more time before it can start accepting commands
  Write-Host "Sleeping for 30 seconds to let the container finish initializing..." -ForegroundColor Yellow
  Start-Sleep -Seconds 30

  # Check to see what containers are running
  docker ps
}

Set-Location $PSScriptRoot

if ($StartMSSqlContainer)
{
  $mssqlPwd = $env:MSSQL_SA_PASSWORD
  if (!$mssqlPwd) {
    Write-Warning "No MSSQL_SA_PASSWORD environment variable found! Skipping SQL Server container startup."
  }
  else {
    StartMSSQLContainer $mssqlPwd
  }
}

if ($StartDTSContainer)
{
    StartDTSContainer
}

StopOnFailedExecution
