#Requires -Version 7.0
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

$ErrorActionPreference = 'Stop'
$runner = Join-Path $PSScriptRoot 'run-smoke-tests.ps1'
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) "durable-smoke-runner-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $testDirectory | Out-Null

function Test-SmokeRunner {
    param(
        [string] $Name,
        [bool] $TriggerFault,
        [int] $SignalFailures = 0,
        [int] $StatusFailures = 0,
        [int] $ExpectedSignals,
        [int] $ExpectedHostStarts,
        [bool] $ExpectFailure = $false
    )

    $scenario = @{
        Creations = 0
        Signals = 0
        HostStarts = 0
        SignalFailures = $SignalFailures
        StatusFailures = $StatusFailures
        FaultAccepted = $false
        Completed = $false
        TriggerFault = $TriggerFault
        ProcessIds = [Collections.Generic.List[int]]::new()
        LogPaths = [Collections.Generic.HashSet[string]]::new()
    }

    # Mock the host's HTTP contract, but run the actual smoke runner and process cleanup.
    function Invoke-RestMethod {
        param([string] $Uri, [string] $Method, [int] $TimeoutSec, [string] $ContentType, [string] $Body)

        switch -Regex ($Uri) {
            '/admin/host/status$' { return @{ state = 'Running' } }
            '/admin/host/ping$' { return }
            '/api/TestStart$' {
                $scenario.Creations++
                return @{
                    id = 'only-instance'
                    statusQueryGetUri = 'http://localhost:7071/status/only-instance'
                    sendEventPostUri = 'http://localhost:7071/events/only-instance/{eventName}'
                }
            }
            '/events/only-instance/StartFault$' {
                $scenario.Signals++
                if ($scenario.SignalFailures -gt 0) {
                    $scenario.SignalFailures--
                    throw [Net.Http.HttpRequestException]::new('Injected StartFault failure before acceptance.')
                }
                $scenario.FaultAccepted = $true
                return
            }
            '/status/only-instance$' {
                if ($scenario.StatusFailures -gt 0) {
                    $scenario.StatusFailures--
                    throw [Net.Http.HttpRequestException]::new('Injected status request failure.')
                }
                if ($scenario.TriggerFault -and -not $scenario.FaultAccepted) {
                    return @{ runtimeStatus = 'Running' }
                }
                $scenario.Completed = $true
                return @{ runtimeStatus = 'Completed' }
            }
            default { throw "Unexpected HTTP request: $Method $Uri" }
        }
    }

    function Start-Process {
        param(
            [string] $FilePath, [string[]] $ArgumentList, [string] $WorkingDirectory,
            [string] $RedirectStandardOutput, [string] $RedirectStandardError, [switch] $PassThru
        )

        if ($FilePath -ne 'func') { throw "Unexpected process: $FilePath" }
        $scenario.HostStarts++
        $null = $scenario.LogPaths.Add($RedirectStandardOutput)
        $null = $scenario.LogPaths.Add($RedirectStandardError)
        $pwsh = (Get-Process -Id $PID).Path
        $child = Microsoft.PowerShell.Management\Start-Process -FilePath $pwsh `
            -ArgumentList '-NoProfile', '-NonInteractive', '-Command', '[Threading.Thread]::Sleep(-1)' `
            -RedirectStandardOutput $RedirectStandardOutput -RedirectStandardError $RedirectStandardError -PassThru
        $scenario.ProcessIds.Add($child.Id)
        return $child
    }

    function Start-Sleep {
        param([int] $Seconds)
    }

    $failure = $null
    try {
        try {
            $null = & $runner -AppDirectory $testDirectory -HttpStartPath 'api/TestStart' -TriggerFault:$TriggerFault 6>&1
        } catch {
            $failure = $_
        }

        if ($ExpectFailure) {
            if ($null -eq $failure -or $failure.Exception.Message -notlike '*StartFault failure*' -or $scenario.Completed) {
                throw "$Name did not report the expected repeated signal failure."
            }
        } elseif ($null -ne $failure) {
            throw $failure
        } elseif (-not $scenario.Completed) {
            throw "$Name did not reach completion."
        }

        if ($scenario.Creations -ne 1 -or $scenario.Signals -ne $ExpectedSignals -or $scenario.HostStarts -ne $ExpectedHostStarts) {
            throw "$Name counts: creations=$($scenario.Creations), signals=$($scenario.Signals), hosts=$($scenario.HostStarts)."
        }
        foreach ($processId in $scenario.ProcessIds) {
            if (Get-Process -Id $processId -ErrorAction SilentlyContinue) {
                throw "$Name left host PID $processId running."
            }
        }
        Write-Host "Passed: $Name"
    } finally {
        foreach ($processId in $scenario.ProcessIds) {
            if (Get-Process -Id $processId -ErrorAction SilentlyContinue) {
                Stop-Process -Id $processId -Force
            }
        }
        foreach ($logPath in $scenario.LogPaths) {
            if (Test-Path -LiteralPath $logPath) { Remove-Item -LiteralPath $logPath -Force }
        }
    }
}

try {
    foreach ($file in @('host.json', 'worker.config.json', 'functions.metadata', 'DotNetIsolated.runtimeconfig.json')) {
        Set-Content -LiteralPath (Join-Path $testDirectory $file) -Value '{}'
    }

    Test-SmokeRunner -Name 'Retry failed StartFault without recreating the instance' `
        -TriggerFault $true -SignalFailures 1 -ExpectedSignals 2 -ExpectedHostStarts 2
    Test-SmokeRunner -Name 'Do not resend an accepted fault after polling recovery' `
        -TriggerFault $true -StatusFailures 1 -ExpectedSignals 1 -ExpectedHostStarts 2
    Test-SmokeRunner -Name 'Signal once on the fault happy path' `
        -TriggerFault $true -ExpectedSignals 1 -ExpectedHostStarts 1
    Test-SmokeRunner -Name 'Do not signal ordinary orchestrations' `
        -TriggerFault $false -ExpectedSignals 0 -ExpectedHostStarts 1
    Test-SmokeRunner -Name 'Fail after the permitted recovery is exhausted' `
        -TriggerFault $true -SignalFailures 2 -ExpectedSignals 2 -ExpectedHostStarts 2 -ExpectFailure $true
} finally {
    Remove-Item -LiteralPath $testDirectory -Recurse -Force
}
