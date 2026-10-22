#Requires -Version 7.0
# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for license information.

[CmdletBinding()]
param(
    [string] $Driver = (Join-Path $PSScriptRoot 'Validate-LargePayloadPurge.ps1'),
    [string] $ResultsPath
)

$ErrorActionPreference = 'Stop'
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) "durable-purge-driver-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $testDirectory | Out-Null

function Assert-Condition([bool] $Condition, [string] $Message) {
    if (!$Condition) { throw $Message }
}

function Test-DriverScenario {
    param(
        [string] $Name,
        [ValidateSet('Prepare', 'Purge')][string] $Mode,
        [string[]] $Faults = @(),
        [bool] $ExpectFailure = $false,
        [string] $ExpectedFailure = '',
        [bool] $ExactFailure = $false,
        [bool] $ExpectCleanup = $false,
        [bool] $ExpectCleanupReport = $false,
        [bool] $UseMissingReference = $false
    )

    $scenarioDirectory = Join-Path $testDirectory ($Name -replace '[^a-zA-Z0-9.-]', '-')
    $scenario = @{
        Calls = [Collections.Generic.List[string]]::new()
        Evidence = [Collections.Generic.List[string]]::new()
        StorageCalls = 0
    }

    function Invoke-RestMethod {
        param([uri] $Uri, [string] $Method, [int] $TimeoutSec)

        $verb = if ($Method) { $Method.ToUpperInvariant() } else { 'GET' }
        $path = $Uri.AbsolutePath
        $scenario.Calls.Add("$verb $path")
        switch -Regex ($path) {
            '^/admin/functions$' {
                if ('BeforeEnable' -in $Faults) { throw 'Injected pre-enable failure.' }
                return @(
                    @{ name = 'BlobPurgeJobOrchestrator'; config = @{ scriptFile = 'Microsoft.Azure.Functions.Worker.Extensions.DurableTask.AzureBlobPayloads.dll' } },
                    @{ name = 'GetLargePayloadTombstonesActivity'; config = @{ scriptFile = 'Microsoft.Azure.Functions.Worker.Extensions.DurableTask.AzureBlobPayloads.dll' } },
                    @{ name = 'DeleteExternalBlobActivity'; config = @{ scriptFile = 'Microsoft.Azure.Functions.Worker.Extensions.DurableTask.AzureBlobPayloads.dll' } },
                    @{ name = 'ReportLargePayloadPurgeResultsActivity'; config = @{ scriptFile = 'Microsoft.Azure.Functions.Worker.Extensions.DurableTask.AzureBlobPayloads.dll' } }
                )
            }
            '^/api/large-payload-purge/storage$' {
                $scenario.StorageCalls++
                if ($scenario.StorageCalls -eq 1) { return @{ blobs = @() } }
                if ($scenario.StorageCalls -eq 2) { return @{ blobs = @(@{ name = 'payload-a' }) } }
                return @{ blobs = @() }
            }
            '^/api/large-payload-purge/true$' {
                if ('Enable' -in $Faults) { throw 'Injected enable response failure.' }
                return @{ workerPid = 1234 }
            }
            '^/api/large-payload-purge/payload$' {
                if ('Payload' -in $Faults) { throw 'Injected payload creation failure.' }
                return @{ instanceId = 'instance-a'; workerPid = 1234 }
            }
            '^/api/large-payload-purge/instances/instance-a$' {
                return @{ status = 'Completed'; instanceId = 'instance-a' }
            }
            '^/api/large-payload-purge/instances/instance-a/purge$' {
                return @{ purged = $true }
            }
            '^/api/large-payload-purge/override/false$' {
                if ('OverrideDisable' -in $Faults) { throw 'Injected override disable failure.' }
                return @{ enabled = $false }
            }
            '^/api/large-payload-purge/false$' {
                if ('DefaultDisable' -in $Faults) { throw 'Injected default disable failure.' }
                return @{ enabled = $false }
            }
            default { throw "Unexpected HTTP request: $verb $path" }
        }
    }

    function Set-Content {
        param(
            [Parameter(Mandatory = $true, Position = 0)][string] $Path,
            [Parameter(ValueFromPipeline = $true)] $Value
        )

        process {
            $name = Split-Path -Leaf $Path
            $scenario.Evidence.Add($name)
            if ('EnabledEvidence' -in $Faults -and $name -eq 'enabled.json') {
                throw 'Injected enable evidence failure.'
            }
            if ('OverrideEvidence' -in $Faults -and $name -eq 'override-disabled.json') {
                throw 'Injected override evidence failure.'
            }
            Microsoft.PowerShell.Management\Set-Content -Path $Path -Value $Value
        }
    }

    function Start-Sleep {
        param([int] $Milliseconds, [int] $Seconds)
    }

    $arguments = @{
        BaseUri = [uri] 'http://127.0.0.1:7071'
        ArtifactsDirectory = $scenarioDirectory
    }
    if ($Mode -eq 'Purge') {
        $arguments.InstanceId = 'instance-a'
        $arguments.ReferencedBlobNames = if ($UseMissingReference) { @('missing') } else { @('payload-a') }
    }

    $records = [Collections.Generic.List[object]]::new()
    $failure = $null
    try {
        & $Driver @arguments 2>&1 | ForEach-Object { $records.Add($_) }
    } catch {
        $failure = $_
    }

    Assert-Condition (($null -ne $failure) -eq $ExpectFailure) "$Name failure expectation was not met."
    if ($ExpectedFailure) {
        $failureMatches = if ($ExactFailure) {
            $failure.Exception.Message -eq $ExpectedFailure
        } else {
            $failure.Exception.Message -like "*$ExpectedFailure*"
        }
        Assert-Condition $failureMatches "$Name failure '$($failure.Exception.Message)' did not match '$ExpectedFailure'."
    }

    $overrideDisables = @($scenario.Calls | Where-Object { $_ -eq 'POST /api/large-payload-purge/override/false' }).Count
    $defaultDisables = @($scenario.Calls | Where-Object { $_ -eq 'POST /api/large-payload-purge/false' }).Count
    $expectedDisableCount = if ($ExpectCleanup) { 1 } else { 0 }
    Assert-Condition ($overrideDisables -eq $expectedDisableCount) `
        "$Name override disable count was $overrideDisables, expected $expectedDisableCount."
    Assert-Condition ($defaultDisables -eq $expectedDisableCount) `
        "$Name default disable count was $defaultDisables, expected $expectedDisableCount."

    $cleanupReports = @($records | Where-Object { $_ -is [Management.Automation.ErrorRecord] -and $_.ToString() -like '*cleanup failed*' })
    Assert-Condition (($cleanupReports.Count -gt 0) -eq $ExpectCleanupReport) `
        "$Name cleanup reporting expectation was not met."

    if ($Name -eq 'Prepare success leaves purge enabled') {
        Assert-Condition (@($records | ForEach-Object ToString | Where-Object { $_ -like '*Prepared instance*' }).Count -gt 0) `
            "$Name did not print the preparation result."
    }
    if ($Name -eq 'Purge success disables both hubs') {
        Assert-Condition (@($records | ForEach-Object ToString | Where-Object { $_ -like '*removed all 1 observed referenced payload blobs*' }).Count -gt 0) `
            "$Name did not print the purge result."
    }
    if ('OverrideEvidence' -in $Faults) {
        Assert-Condition ('disabled.json' -in $scenario.Evidence) `
            "$Name did not save default disable evidence after override evidence failed."
    }
    if ($ExpectCleanupReport) {
        Assert-Condition (@($records | ForEach-Object ToString | Where-Object { $_ -like '*removed all*' }).Count -eq 0) `
            "$Name printed a success result after cleanup failed."
    }

    return [ordered]@{
        name = $Name
        passed = $true
        failure = if ($failure) { $failure.Exception.Message } else { $null }
        calls = @($scenario.Calls)
        evidence = @($scenario.Evidence)
        output = @($records | ForEach-Object ToString)
    }
}

$results = [Collections.Generic.List[object]]::new()
$failures = [Collections.Generic.List[string]]::new()
$scenarios = @(
    @{ Name = 'Pre-enable failure does not disable'; Mode = 'Prepare'; Faults = @('BeforeEnable'); ExpectFailure = $true; ExpectedFailure = 'Injected pre-enable failure' },
    @{ Name = 'Prepare success leaves purge enabled'; Mode = 'Prepare' },
    @{ Name = 'Failed Prepare disables both hubs'; Mode = 'Prepare'; Faults = @('Payload'); ExpectFailure = $true; ExpectedFailure = 'Injected payload creation failure'; ExpectCleanup = $true },
    @{ Name = 'Purge success disables both hubs'; Mode = 'Purge'; ExpectCleanup = $true },
    @{ Name = 'Early Purge reference failure disables both hubs'; Mode = 'Purge'; UseMissingReference = $true; ExpectFailure = $true; ExpectedFailure = 'Referenced payload blob missing'; ExpectCleanup = $true },
    @{ Name = 'Enable response failure still disables both hubs'; Mode = 'Prepare'; Faults = @('Enable'); ExpectFailure = $true; ExpectedFailure = 'Injected enable response failure'; ExpectCleanup = $true },
    @{ Name = 'Enable evidence failure still disables both hubs'; Mode = 'Prepare'; Faults = @('EnabledEvidence'); ExpectFailure = $true; ExpectedFailure = 'Injected enable evidence failure.'; ExactFailure = $true; ExpectCleanup = $true },
    @{ Name = 'Override disable failure still attempts default'; Mode = 'Purge'; Faults = @('OverrideDisable'); ExpectFailure = $true; ExpectedFailure = 'Cleanup failed'; ExpectCleanup = $true; ExpectCleanupReport = $true },
    @{ Name = 'Default disable failure fails Purge'; Mode = 'Purge'; Faults = @('DefaultDisable'); ExpectFailure = $true; ExpectedFailure = 'Cleanup failed'; ExpectCleanup = $true; ExpectCleanupReport = $true },
    @{ Name = 'Original failure survives cleanup failure'; Mode = 'Prepare'; Faults = @('Payload', 'OverrideDisable'); ExpectFailure = $true; ExpectedFailure = 'Injected payload creation failure.'; ExactFailure = $true; ExpectCleanup = $true; ExpectCleanupReport = $true },
    @{ Name = 'Override evidence failure still attempts default'; Mode = 'Purge'; Faults = @('OverrideEvidence'); ExpectFailure = $true; ExpectedFailure = 'Cleanup failed'; ExpectCleanup = $true; ExpectCleanupReport = $true }
)

try {
    foreach ($scenario in $scenarios) {
        try {
            $results.Add((Test-DriverScenario @scenario))
            Write-Host "Passed: $($scenario.Name)"
        } catch {
            $failures.Add("$($scenario.Name): $($_.Exception.Message)")
            $results.Add([ordered]@{
                name = $scenario.Name
                passed = $false
                assertion = $_.Exception.Message
            })
            Write-Host "Failed: $($scenario.Name): $($_.Exception.Message)"
        }
    }

    $summary = [ordered]@{
        driver = (Resolve-Path $Driver).Path
        total = $scenarios.Count
        passed = @($results | Where-Object passed).Count
        failed = $failures.Count
        scenarios = @($results)
    }
    if ($ResultsPath) {
        $parent = Split-Path -Parent $ResultsPath
        if ($parent) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
        $summary | ConvertTo-Json -Depth 20 | Microsoft.PowerShell.Management\Set-Content -Path $ResultsPath
    }

    if ($failures.Count -gt 0) {
        throw "Driver regression failures:`n$($failures -join "`n")"
    }

    $summary | ConvertTo-Json -Depth 20
} finally {
    Remove-Item -LiteralPath $testDirectory -Recurse -Force
}
