# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

# CI-only: deletes generated output in each selected app after its suite, including on failure.
# Core Tools must already be installed and backend emulators started; both span all suites.
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('net8.0', 'net10.0')]
    [string] $TargetFramework,

    [Parameter(Mandatory)]
    [ValidateSet('AzureStorage', 'DTS', 'MSSQL')]
    [string] $Backend,

    [Parameter(Mandatory)]
    [ValidateSet('BasicDotNetIsolated', 'BasicPowerShell', 'BasicPython', 'BasicNode', 'BasicJava')]
    [string[]] $E2EAppName,

    [Parameter(Mandatory)]
    [string] $ResultsDirectory,

    [ValidateRange(1, 120)]
    [int] $HostStopTimeoutSeconds = 30
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
if ($env:TF_BUILD -ne 'True') {
    throw 'This destructive cleanup runner is for CI only (TF_BUILD=True). Use build-e2e-test.ps1 locally.'
}

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$testProject = Join-Path $repo 'test\e2e\Tests\E2ETests.csproj'
$buildScript = Join-Path $repo 'test\e2e\Tests\build-e2e-test.ps1'
$appsRoot = Join-Path $repo 'test\e2e\Apps'
$ResultsDirectory = [IO.Path]::GetFullPath($ResultsDirectory)
$comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
if (($ResultsDirectory + [IO.Path]::DirectorySeparatorChar).StartsWith($appsRoot + [IO.Path]::DirectorySeparatorChar, $comparison)) {
    throw 'ResultsDirectory must be outside the app directories being cleaned.'
}
New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null
$script:exitCode = 0

function Protect-Log([string] $Text) {
    if ($env:MSSQL_SA_PASSWORD) {
        $Text = $Text.Replace($env:MSSQL_SA_PASSWORD, '***')
        $Text = $Text.Replace([Uri]::EscapeDataString($env:MSSQL_SA_PASSWORD), '***')
    }
    return $Text
}

function Write-DiskUsage([string] $Phase) {
    $drive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot($appsRoot))
    $message = '{0}: disk free {1:N2} GiB / {2:N2} GiB' -f $Phase, ($drive.AvailableFreeSpace / 1GB), ($drive.TotalSize / 1GB)
    Write-Host $message
    Add-Content -LiteralPath (Join-Path $ResultsDirectory 'disk-usage.log') -Value $message
}

function Invoke-LoggedPhase([string] $Name, [scriptblock] $Command) {
    $log = Join-Path $ResultsDirectory "$Name.log"
    $global:LASTEXITCODE = 0
    try {
        & $Command 2>&1 | ForEach-Object {
            $line = Protect-Log "$_"
            Add-Content -LiteralPath $log -Value $line
            Write-Host $line
        }
        $code = $LASTEXITCODE
        if ($code) {
            $script:exitCode = $code
            throw "$Name failed (exit $code). See $log"
        }
    } catch {
        Add-Content -LiteralPath $log -Value (Protect-Log "$_")
        throw
    }
}

function Test-HostListening {
    foreach ($address in @([Net.IPAddress]::Loopback, [Net.IPAddress]::IPv6Loopback)) {
        $client = [Net.Sockets.TcpClient]::new($address.AddressFamily)
        try {
            $connection = $client.BeginConnect($address, 7071, $null, $null)
            # Windows can take ~2s to report connection refused even on loopback.
            if ($connection.AsyncWaitHandle.WaitOne(3000)) {
                try {
                    $client.EndConnect($connection)
                    return $true
                } catch [Net.Sockets.SocketException] {
                    if ($_.Exception.SocketErrorCode -ne [Net.Sockets.SocketError]::ConnectionRefused) { throw }
                    # Connection refused: this loopback address has no host listening.
                }
            } else {
                # An inconclusive probe must not authorize deleting a running host's files.
                return $true
            }
        } finally {
            $client.Dispose()
        }
    }
    return $false
}

function Wait-HostStopped {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while (Test-HostListening) {
        if ($timer.Elapsed.TotalSeconds -ge $HostStopTimeoutSeconds) {
            throw "Port 7071 is still listening after ${HostStopTimeoutSeconds}s; refusing app cleanup or another suite. Fixture teardown must stop its owned host."
        }
        Start-Sleep -Milliseconds 250
    }
    Write-Host 'Host stopped: port 7071 is closed on IPv4 and IPv6 loopback.'
}

function Remove-AppOutput($Case) {
    $app = Join-Path $appsRoot $Case.App
    foreach ($directory in $Case.Output) {
        $path = Join-Path $app $directory
        if (Test-Path -LiteralPath $path) {
            # Do not follow a redirected app/output directory outside this app's allowlist.
            if (((Get-Item -LiteralPath $app).Attributes -band [IO.FileAttributes]::ReparsePoint) -or
                ((Get-Item -LiteralPath $path -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                throw "Refusing cleanup of redirected directory: $path"
            }
            Write-Host "Removing generated output: $path"
            Remove-Item -LiteralPath $path -Recurse -Force -ErrorAction Stop
        }
    }
}

$cases = @(
    @{ Name = 'dotnet-isolated'; App = 'BasicDotNetIsolated'; Filter = 'Dotnet'; Output = @('bin', 'obj', 'packages') }
    @{ Name = 'powershell'; App = 'BasicPowerShell'; Filter = 'PowerShell'; Output = @('bin', 'obj', 'packages', 'Modules') }
    @{ Name = 'python'; App = 'BasicPython'; Filter = 'Python'; Output = @('bin', 'obj', 'packages', '.venv') }
    @{ Name = 'node'; App = 'BasicNode'; Filter = 'Node'; Output = @('bin', 'obj', 'packages', 'node_modules', 'dist') }
    @{ Name = 'java'; App = 'BasicJava'; Filter = 'Java'; Output = @('bin', 'obj', 'packages', 'target') }
)
$cases = @($cases | Where-Object { $_.App -in $E2EAppName })
$previousLanguage = $env:E2E_TEST_FUNCTIONS_LANGUAGE
$previousApp = $env:TEST_APP_NAME
Push-Location $repo
try {
    Wait-HostStopped
    Write-DiskUsage 'Before test project build'
    # E2ETests references WebJobs.Extensions.DurableTask. Build once, keep its outputs for all suites.
    Invoke-LoggedPhase "test-project-$Backend-$TargetFramework-build" {
        dotnet build $testProject -f $TargetFramework
    }
    Write-DiskUsage 'After test project build'

    foreach ($case in $cases) {
        $prefix = "language-e2e-$Backend-$TargetFramework-$($case.Name)"
        try {
            Write-DiskUsage "$($case.Name): before app build"
            Invoke-LoggedPhase "$prefix-build" {
                # The helper changes cwd on exit. Always restore it, even when a native build fails.
                Push-Location $repo
                try {
                    & $buildScript -TargetFramework $TargetFramework -E2EAppName $case.App `
                        -SkipCoreTools -SkipStorageEmulator -SkipExtensionBuild -PreserveNuGetCache
                } finally {
                    Pop-Location
                }
            }
            Write-DiskUsage "$($case.Name): after app build"
            $env:E2E_TEST_FUNCTIONS_LANGUAGE = $case.Name
            $env:TEST_APP_NAME = $case.App
            $filter = "$Backend!=Skip&$($case.Filter)!=Skip&$($case.Filter)-$Backend!=Skip"
            Invoke-LoggedPhase "$prefix-test" {
                dotnet test $testProject -f $TargetFramework --no-build --filter $filter `
                    --logger "trx;LogFileName=$prefix.trx" --logger 'console;verbosity=normal' `
                    --results-directory $ResultsDirectory
            }
            Write-DiskUsage "$($case.Name): after tests"
        } finally {
            try {
                # dotnet test awaits fixture disposal (which kills its process tree). Never kill by name here.
                Wait-HostStopped
                Write-DiskUsage "$($case.Name): before cleanup"
                Remove-AppOutput $case
                Write-DiskUsage "$($case.Name): after cleanup"
            } catch {
                $message = Protect-Log "Cleanup failed for $($case.App): $_"
                Add-Content -LiteralPath (Join-Path $ResultsDirectory "$prefix-cleanup.log") -Value $message
                Write-Warning $message
                if (!$script:exitCode) { $script:exitCode = 1 }
            }
        }
        if ($script:exitCode) { break }
    }
} catch {
    $message = Protect-Log "$_"
    Add-Content -LiteralPath (Join-Path $ResultsDirectory 'runner-failure.log') -Value $message
    Write-Warning $message
    if (!$script:exitCode) { $script:exitCode = 1 }
} finally {
    $env:E2E_TEST_FUNCTIONS_LANGUAGE = $previousLanguage
    $env:TEST_APP_NAME = $previousApp
    Pop-Location
}
exit $script:exitCode
