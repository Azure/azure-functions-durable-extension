# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param(
    [Parameter(Mandatory=$false)]
    [Switch]
    $SkipStorageEmulator,
    [Parameter(Mandatory=$false)]
    $EmulatorStartDir,
    [Parameter(Mandatory=$false)]
    [Switch]
    $NoWait
)

if ($PSVersionTable.PSEdition -ne 'Core') {
    Write-Warning "You are not running PowerShell Core. Please switch to PowerShell Core (>= PS 6) for better compatibility and performance."
    Write-Warning "See https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-7.5"
    exit 1
}

if (Test-Path($EmulatorStartDir)) {
    Set-Location $EmulatorStartDir
}

$DebugPreference = 'Continue'

Write-Host "Skip Storage Emulator: $SkipStorageEmulator"

$startedStorage = $false

function IsStorageEmulatorRunning()
{
    try
    {
        $response = Invoke-WebRequest -Uri "http://127.0.0.1:10000/"
        $StatusCode = $Response.StatusCode
    }
    catch
    {
        $StatusCode = $_.Exception.Response.StatusCode.value__
    }

    if ($StatusCode -eq 400)
    {
        return $true
    }

    return $false
}

if (!$SkipStorageEmulator)
{
    Write-Host "------"
    Write-Host ""
    Write-Host "---Starting Storage emulator---"
    $storageEmulatorRunning = IsStorageEmulatorRunning

    if ($storageEmulatorRunning -eq $false)
    {
        if ($IsWindows)
        {
            npm install -g azurite
            New-Item -Path "./azurite" -ItemType Directory -ErrorAction SilentlyContinue
            
            # Get the npm global bin path to find azurite
            $npmPrefix = (npm config get prefix).Trim()
            $azuritePath = Join-Path $npmPrefix "azurite.cmd"
            
            if (!(Test-Path $azuritePath)) {
                # Fallback to just using the command name if it's in PATH
                $azuritePath = "azurite.cmd"
            }
            
            Write-Host "Starting Azurite from: $azuritePath"
            Start-Process $azuritePath -WorkingDirectory "./azurite" -ArgumentList "--silent" -NoNewWindow
            
            # Give Azurite a moment to initialize before we start polling
            Start-Sleep -Seconds 3
        }
        else
        {
            sudo npm install -g azurite
            New-Item -Path "./azurite" -ItemType Directory -ErrorAction SilentlyContinue
            sudo azurite --silent --location azurite --debug azurite\debug.log &
        }

        $startedStorage = $true
    }
    else
    {
        Write-Host "Storage emulator is already running."
    }

    Write-Host "------"
    Write-Host
}

if ($NoWait -eq $true)
{
    Write-Host "'NoWait' specified. Exiting."
    Write-Host
    exit 0
}

if (!$SkipStorageEmulator -and $startedStorage -eq $true)
{
    Write-Host "---Waiting for Storage emulator to be running---"
    $storageEmulatorRunning = IsStorageEmulatorRunning
    $maxWaitSeconds = 120
    $waitedSeconds = 0
    while ($storageEmulatorRunning -eq $false)
    {
        Write-Host "Storage emulator not ready. Waited $waitedSeconds seconds..."
        Start-Sleep -Seconds 5
        $waitedSeconds += 5
        
        if ($waitedSeconds -ge $maxWaitSeconds) {
            Write-Error "Storage emulator failed to start within $maxWaitSeconds seconds"
            exit 1
        }
        
        $storageEmulatorRunning = IsStorageEmulatorRunning
    }
    Write-Host "Storage emulator ready."
    Write-Host "------"
    Write-Host
}