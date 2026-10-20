# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

# Standalone orchestration tests, without an E2E host, package downloads, or backend containers.
# Run with: pwsh -File eng\ci\templates\public\test-run-language-e2e.ps1
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$sandbox = Join-Path $repo "TestResults\language-runner-$([guid]::NewGuid().ToString('N'))"
$pwsh = (Get-Process -Id $PID).Path
$allApps = @('BasicDotNetIsolated', 'BasicPowerShell', 'BasicPython', 'BasicNode', 'BasicJava')
$runnerRelative = 'eng\ci\templates\public\run-language-e2e.ps1'
$previousCI = $env:TF_BUILD
$previousPassword = $env:MSSQL_SA_PASSWORD

function Assert($Condition, [string] $Message) {
    if (!$Condition) { throw $Message }
}

try {
    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    # Use an actual child native process so LASTEXITCODE and redirected stderr are exercised.
    $mock = Join-Path $sandbox 'mock-dotnet.ps1'
    @'
$ErrorActionPreference = 'Stop'
$root = $env:RUNNER_TEST_ROOT
$events = Join-Path $root 'events.txt'
if ($args[0] -eq 'build') {
    Add-Content $events 'project-build'
    if ($env:RUNNER_TEST_SCENARIO -eq 'project-failure') { exit 17 }
    exit 0
}
if ($args[0] -ne 'test') { throw 'Unexpected dotnet command' }
if (!(Test-Path $args[1] -PathType Leaf)) { throw 'Test project path was not absolute/valid' }
if ($PWD.Path -ne $root) { throw 'Build helper leaked its working directory' }
$app = $env:TEST_APP_NAME
$filterIndex = [array]::IndexOf($args, '--filter')
$frameworkIndex = [array]::IndexOf($args, '-f')
$languageFilter = @{ BasicDotNetIsolated = 'Dotnet'; BasicPowerShell = 'PowerShell'; BasicPython = 'Python'; BasicNode = 'Node'; BasicJava = 'Java' }[$app]
$backend = $env:RUNNER_TEST_BACKEND
$expectedFilter = "$backend!=Skip&$languageFilter!=Skip&$languageFilter-$backend!=Skip"
if ($args[$filterIndex + 1] -ne $expectedFilter -or $args[$frameworkIndex + 1] -ne $env:RUNNER_TEST_FRAMEWORK -or '--no-build' -notin $args) { throw 'Incorrect test filters/framework/build flags' }
Add-Content $events "test:$app"
$resultIndex = [array]::IndexOf($args, '--results-directory')
$trxArgument = $args | Where-Object { $_ -like 'trx;LogFileName=*' }
Set-Content (Join-Path $args[$resultIndex + 1] $trxArgument.Substring('trx;LogFileName='.Length)) 'xUnit failure output'
Write-Output "test console password=$env:MSSQL_SA_PASSWORD"
[Console]::Error.WriteLine('test stderr diagnostics')
if ($env:RUNNER_TEST_SCENARIO -eq 'lingering-host') { exit 13 }
if ($env:RUNNER_TEST_SCENARIO -in @('test-failure', 'combined-failure')) { exit 13 }
exit 0
'@ | Set-Content -LiteralPath $mock
    $env:RUNNER_TEST_MOCK = $mock
    $env:TF_BUILD = 'True'
    $env:MSSQL_SA_PASSWORD = 'runner-test-secret!'
    $env:RUNNER_TEST_PWSH = $pwsh
    $scenarios = @(
        @{ Name = 'all-linux'; Apps = $allApps; Backend = 'AzureStorage'; Framework = 'net8.0'; Exit = 0 }
        @{ Name = 'dotnet-shard'; Apps = @('BasicDotNetIsolated'); Backend = 'DTS'; Framework = 'net10.0'; Exit = 0 }
        @{ Name = 'node-shard'; Apps = @('BasicNode'); Backend = 'MSSQL'; Framework = 'net10.0'; Exit = 0 }
        @{ Name = 'other-shard'; Apps = @('BasicPowerShell', 'BasicPython', 'BasicJava'); Backend = 'AzureStorage'; Framework = 'net10.0'; Exit = 0 }
        @{ Name = 'build-failure'; Exit = 7 }
        @{ Name = 'test-failure'; Exit = 13 }
        @{ Name = 'cleanup-failure'; Exit = 1 }
        @{ Name = 'combined-failure'; Exit = 13 }
        @{ Name = 'lingering-host'; Exit = 13 }
        @{ Name = 'project-failure'; Exit = 17 }
        @{ Name = 'local-refused'; Exit = 1 }
        @{ Name = 'delayed-host'; Exit = 0 }
        @{ Name = 'results-refused'; Exit = 1 }
    )
    foreach ($scenario in $scenarios) {
        $root = Join-Path $sandbox $scenario.Name
        $env:RUNNER_TEST_ROOT = $root
        $env:RUNNER_TEST_SCENARIO = $scenario.Name
        $env:RUNNER_TEST_BACKEND = if ($scenario.Backend) { $scenario.Backend } else { 'MSSQL' }
        $env:RUNNER_TEST_FRAMEWORK = if ($scenario.Framework) { $scenario.Framework } else { 'net8.0' }
        $apps = if ($scenario.Apps) { $scenario.Apps } else { @('BasicNode', 'BasicJava') }
        $results = Join-Path $root 'results'
        if ($scenario.Name -eq 'results-refused') { $results = Join-Path $root 'test\e2e\Apps\BasicNode\bin\results' }
        New-Item -ItemType Directory -Path (Split-Path (Join-Path $root $runnerRelative)), (Join-Path $root 'test\e2e\Tests'), $results -Force | Out-Null
        Copy-Item (Join-Path $repo $runnerRelative) (Join-Path $root $runnerRelative)
        Set-Content (Join-Path $root 'test\e2e\Tests\E2ETests.csproj') 'mock project'
        Set-Content (Join-Path $root 'shared-cache.txt') 'keep cached packages and tools'
        foreach ($app in $allApps) {
            $directory = Join-Path $root "test\e2e\Apps\$app"
            New-Item -ItemType Directory -Path $directory -Force | Out-Null
            Set-Content (Join-Path $directory 'host.json') 'keep settings'
            Set-Content (Join-Path $directory 'source.txt') 'keep source'
            New-Item -ItemType Directory -Path (Join-Path $directory 'user-directory') | Out-Null
            if ($app -notin $apps) {
                New-Item -ItemType Directory -Path (Join-Path $directory 'bin') -Force | Out-Null
                Set-Content (Join-Path $directory 'bin\unselected.txt') 'keep other apps'
            }
        }
        @'
param($TargetFramework, $E2EAppName, [switch]$SkipCoreTools, [switch]$SkipStorageEmulator, [switch]$SkipExtensionBuild, [switch]$PreserveNuGetCache)
$ErrorActionPreference = 'Stop'
if (!$SkipCoreTools -or !$SkipStorageEmulator -or !$SkipExtensionBuild -or !$PreserveNuGetCache) { throw 'Missing CI reuse flags' }
$root = $env:RUNNER_TEST_ROOT
if ($TargetFramework -ne $env:RUNNER_TEST_FRAMEWORK) { throw 'Incorrect app framework' }
$events = Join-Path $root 'events.txt'
if (Test-Path $events) {
    foreach ($previous in (Get-Content $events | Where-Object { $_ -like 'app-build:*' })) {
        $previousApp = $previous.Substring('app-build:'.Length)
        if (Test-Path (Join-Path $root "test\e2e\Apps\$previousApp\bin")) { throw 'Next app started before previous cleanup' }
    }
}
Add-Content $events "app-build:$E2EAppName"
$app = Join-Path $root "test\e2e\Apps\$E2EAppName"
$extra = @{ BasicDotNetIsolated = @(); BasicNode = @('node_modules', 'dist'); BasicPython = @('.venv'); BasicPowerShell = @('Modules'); BasicJava = @('target') }[$E2EAppName]
foreach ($output in (@('bin', 'obj', 'packages') + $extra)) {
    New-Item -ItemType Directory -Path (Join-Path $app $output) -Force | Out-Null
    Set-Content (Join-Path $app "$output\generated.txt") 'generated output'
}
Write-Output "build diagnostics password=$env:MSSQL_SA_PASSWORD"
Set-Location $PSScriptRoot
if ($env:RUNNER_TEST_SCENARIO -eq 'build-failure') { exit 7 }
exit 0
'@ | Set-Content (Join-Path $root 'test\e2e\Tests\build-e2e-test.ps1')
        @'
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 7071)
try {
    $listener.Start()
    Set-Content (Join-Path $PSScriptRoot 'listener.ready') 'ready'
    if ($env:RUNNER_TEST_SCENARIO -eq 'delayed-host') { Start-Sleep -Seconds 2 }
    else { Start-Sleep -Seconds 60 }
} finally {
    $listener.Stop()
    if ($env:RUNNER_TEST_SCENARIO -eq 'delayed-host') {
        Add-Content (Join-Path $PSScriptRoot 'events.txt') 'host-stopped'
    }
}
'@ | Set-Content (Join-Path $root 'listener.ps1')
        $lock = $null
        if ($scenario.Name -in @('cleanup-failure', 'combined-failure')) {
            if (!$IsWindows) { continue } # Unix permits unlinking open files; exercised on Windows.
            $lockedDirectory = Join-Path $root 'test\e2e\Apps\BasicNode\bin'
            New-Item -ItemType Directory -Path $lockedDirectory -Force | Out-Null
            $lock = [IO.File]::Open((Join-Path $lockedDirectory 'locked.txt'), 'Create', 'ReadWrite', 'None')
        }
        if ($scenario.Name -eq 'local-refused') { $env:TF_BUILD = 'False' }
        try {
            # A wrapper passes arrays faithfully to a new pwsh process on Windows and Linux.
            $wrapper = Join-Path $root 'invoke.ps1'
            $timeout = if ($scenario.Name -eq 'delayed-host') { 5 } else { 1 }
            @'
function global:dotnet {
    & $env:RUNNER_TEST_PWSH -NoProfile -File $env:RUNNER_TEST_MOCK @args
    $code = $LASTEXITCODE
    if ($args[0] -eq 'test' -and $env:RUNNER_TEST_SCENARIO -in @('lingering-host', 'delayed-host')) {
        # Start the listener outside the exiting mock process's child-process lifetime.
        $root = $env:RUNNER_TEST_ROOT
        Remove-Item (Join-Path $root 'listener.ready') -ErrorAction SilentlyContinue
        $process = Start-Process $env:RUNNER_TEST_PWSH -ArgumentList '-NoProfile', '-File', "`"$(Join-Path $root 'listener.ps1')`"" -PassThru `
            -RedirectStandardOutput (Join-Path $root 'listener.stdout.log') `
            -RedirectStandardError (Join-Path $root 'listener.stderr.log')
        Set-Content (Join-Path $root 'listener.pid') $process.Id
        for ($i = 0; $i -lt 50 -and !(Test-Path (Join-Path $root 'listener.ready')); $i++) { Start-Sleep -Milliseconds 100 }
        if (!(Test-Path (Join-Path $root 'listener.ready'))) { throw 'Test listener did not start' }
    }
    $global:LASTEXITCODE = $code
}
'@ | Set-Content $wrapper
            "& '$((Join-Path $root $runnerRelative).Replace("'", "''"))' -TargetFramework '$env:RUNNER_TEST_FRAMEWORK' -Backend '$env:RUNNER_TEST_BACKEND' -E2EAppName @('$($apps -join "','")') -ResultsDirectory '$($results.Replace("'", "''"))' -HostStopTimeoutSeconds $timeout`nexit `$LASTEXITCODE" | Add-Content $wrapper
            & $pwsh -NoProfile -File $wrapper *> (Join-Path $root 'invocation.log')
            $code = $LASTEXITCODE
            if ($code -ne $scenario.Exit) { Get-Content (Join-Path $root 'invocation.log') | Write-Host }
            Assert ($code -eq $scenario.Exit) "$($scenario.Name): expected exit $($scenario.Exit), got $code; see $root"
            $events = @(if (Test-Path (Join-Path $root 'events.txt')) { Get-Content (Join-Path $root 'events.txt') })
            $builds = @($events | Where-Object { $_ -like 'app-build:*' })
            $tests = @($events | Where-Object { $_ -like 'test:*' })
            if ($scenario.Exit -eq 0) {
                $hostEvent = if ($scenario.Name -eq 'delayed-host') { ',host-stopped' } else { '' }
                Assert (($events -join ',') -eq ('project-build,' + (($apps | ForEach-Object { "app-build:$_,test:$_$hostEvent" }) -join ','))) "$($scenario.Name): lifecycle order incorrect"
                Assert (@(Get-ChildItem $results -Filter '*.trx').Count -eq $apps.Count) 'TRX files overwritten/missing'
                foreach ($app in $apps) {
                    $remaining = @(Get-ChildItem (Join-Path $root "test\e2e\Apps\$app") -Directory | Where-Object Name -ne 'user-directory')
                    Assert ($remaining.Count -eq 0) "Generated output left for $app"
                }
            } elseif ($scenario.Name -in @('project-failure', 'local-refused', 'results-refused')) {
                Assert ($builds.Count -eq 0 -and $tests.Count -eq 0) 'Apps ran after setup failure'
            } else {
                Assert ($builds.Count -eq 1) 'Runner did not stop after first app failure'
                Assert ($tests.Count -eq [int]($scenario.Name -ne 'build-failure')) 'Unexpected tests after build failure'
                if ($scenario.Name -in @('build-failure', 'test-failure')) {
                    Assert (!(Test-Path (Join-Path $root 'test\e2e\Apps\BasicNode\bin'))) 'Failed app output not cleaned'
                } else {
                    Assert (Test-Path (Join-Path $root 'test\e2e\Apps\BasicNode\bin')) 'Deleted output despite active host/locked file'
                    Assert (Test-Path (Join-Path $results '*-cleanup.log')) 'Cleanup failure not logged'
                }
                if ($scenario.Name -ne 'build-failure') { Assert (@(Get-ChildItem $results -Filter '*.trx').Count -eq 1) 'Failure TRX lost' }
            }
            foreach ($app in $allApps) {
                Assert (Test-Path (Join-Path $root "test\e2e\Apps\$app\host.json")) 'Settings deleted'
                Assert (Test-Path (Join-Path $root "test\e2e\Apps\$app\source.txt")) 'Source deleted'
                Assert (Test-Path (Join-Path $root "test\e2e\Apps\$app\user-directory")) 'Unlisted directory deleted'
                if ($app -notin $apps) {
                    Assert (Test-Path (Join-Path $root "test\e2e\Apps\$app\bin\unselected.txt")) 'Unselected app output deleted'
                }
            }
            Assert (Test-Path (Join-Path $root 'shared-cache.txt')) 'Shared cache removed'
            $logs = (Get-ChildItem $results -Filter '*.log' | Get-Content) -join "`n"
            Assert (!$logs.Contains($env:MSSQL_SA_PASSWORD)) 'Password leaked into retained logs'
            if ($builds.Count) {
                Assert ($logs.Contains('build diagnostics password=***')) 'Build console log missing'
                Assert ($logs.Contains('before app build')) 'Disk measurements missing'
            }
            if ($tests.Count) { Assert ($logs.Contains('test stderr diagnostics')) 'Test stderr missing' }
            Write-Host "PASS $($scenario.Name)"
        } finally {
            if ($lock) { $lock.Dispose() }
            $env:TF_BUILD = 'True'
            $pidFile = Join-Path $root 'listener.pid'
            if (Test-Path $pidFile) {
                $listenerProcess = Get-Process -Id ([int](Get-Content $pidFile)) -ErrorAction SilentlyContinue
                if ($listenerProcess) { Stop-Process -Id $listenerProcess.Id }
            }
        }
    }
    Write-Host 'All orchestration checks passed.'
} finally {
    $env:TF_BUILD = $previousCI
    $env:MSSQL_SA_PASSWORD = $previousPassword
    foreach ($name in @('RUNNER_TEST_ROOT', 'RUNNER_TEST_SCENARIO', 'RUNNER_TEST_BACKEND', 'RUNNER_TEST_FRAMEWORK', 'RUNNER_TEST_PWSH', 'RUNNER_TEST_MOCK')) {
        Remove-Item "Env:\$name" -ErrorAction SilentlyContinue
    }
    if (Test-Path $sandbox) { Remove-Item -LiteralPath $sandbox -Recurse -Force }
}
