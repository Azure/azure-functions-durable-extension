#!/usr/bin/env pwsh
#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

[CmdletBinding()]
param(
    [switch]
    $Clean,

    [Switch]
    $SkipStorageEmulator,

    [Switch]
    $StartCosmosDBEmulator,

    [Switch]
    $SkipCoreTools,

    [Switch]
    $SkipBuildOnPack
)

$ProjectBaseDirectory = "$PSScriptRoot\..\..\..\"
$ProjectTemporaryPath = Join-Path ([System.IO.Path]::GetTempPath()) "DurableTaskExtensionE2ETests"
New-Item -Path $ProjectTemporaryPath -ItemType Directory -ErrorAction SilentlyContinue
$WebJobsExtensionProjectDirectory = Join-Path $ProjectBaseDirectory "src\WebJobs.Extensions.DurableTask"
$WorkerExtensionProjectDirectory = Join-Path $ProjectBaseDirectory "src\Worker.Extensions.DurableTask"
$E2EAppProjectDirectory = Join-Path $ProjectBaseDirectory "test\e2e\Apps\BasicDotNetIsolated"

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
    $coreToolsURL = "https://functionsclibuilds.blob.core.windows.net/builds/$FunctionsRuntimeVersion/latest/Azure.Functions.Cli.$os-$arch.zip"
    $versionUrl = "https://functionsclibuilds.blob.core.windows.net/builds/$FunctionsRuntimeVersion/latest/version.txt"
  }

  Write-Host ""
  Write-Host "---Downloading the Core Tools for Functions V$FunctionsRuntimeVersion---"
  Write-Host "Core Tools download url: $coreToolsURL"

  Write-Host 'Deleting Functions Core Tools if exists...'
  Remove-Item -Force "$FUNC_CLI_DIRECTORY.zip" -ErrorAction Ignore
  Remove-Item -Recurse -Force $FUNC_CLI_DIRECTORY -ErrorAction Ignore

  if ($versionUrl)
  {
    $version = Invoke-RestMethod -Uri $versionUrl
    Write-Host "Downloading Functions Core Tools (Version: $version)..."
  }

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

Write-Host "Removing old packages from test app"

$AppPackageLocation = Join-Path $E2EAppProjectDirectory 'packages'
if (!(Test-Path $AppPackageLocation)) {
  New-Item -Path $AppPackageLocation -ItemType Directory -ErrorAction SilentlyContinue
}
$AppPackageLocation = Resolve-Path $AppPackageLocation
Get-ChildItem -Path $AppPackageLocation -Include * -File -Recurse | ForEach-Object { $_.Delete()}

Write-Host "Building WebJobs extension project"

$BuildOutputLocation = Join-Path $WebJobsExtensionProjectDirectory 'out'
if (!(Test-Path $BuildOutputLocation)) {
  New-Item -Path $BuildOutputLocation -ItemType Directory -ErrorAction SilentlyContinue
}
$BuildOutputLocation = Resolve-Path $BuildOutputLocation
Get-ChildItem -Path $BuildOutputLocation -Include * -File -Recurse | ForEach-Object { $_.Delete()}
dotnet build -c Debug "$WebJobsExtensionProjectDirectory\WebJobs.Extensions.DurableTask.csproj" --output $BuildOutputLocation

Write-Host "Moving nupkg from WebJobs extension to $AppPackageLocation"
Set-Location $BuildOutputLocation
dotnet nuget push *.nupkg --source $AppPackageLocation

Write-Host "Updating app .csproj to reference built package versions"
Set-Location $E2EAppProjectDirectory
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
  }
}

Write-Host "Building app project"
dotnet clean app.csproj
dotnet build app.csproj

Set-Location $PSScriptRoot

if ($SkipStorageEmulator -And -not $StartCosmosDBEmulator)
{
  Write-Host
  Write-Host "---Skipping emulator startup---"
  Write-Host
}
else 
{
  .\start-emulators.ps1 -SkipStorageEmulator:$SkipStorageEmulator -StartCosmosDBEmulator:$StartCosmosDBEmulator -EmulatorStartDir $ProjectTemporaryPath
}

Set-Location $PSScriptRoot

StopOnFailedExecution
