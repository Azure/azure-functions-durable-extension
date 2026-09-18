# This is a simple test runner to validate the .NET isolated smoke tests.
# It supercedes the usual e2e-tests.ps1 script for the .NET isolated scenario because building the snmoke test app
# on the docker image is unreliable. For more details, see: https://github.com/Azure/azure-functions-host/issues/7995

# This script is designed specifically to test cases where the isolated worker process experiences a platform failure:
# timeouts, OOMs, etc. For that reason, it is careful to check that the Functions Host is running and healthy at regular
# intervals. This makes these tests run more slowly than other test categories.
# Fault orchestrations require -TriggerFault, which signals StartFault only after the instance status URL is saved.

param(
	[Parameter(Mandatory=$true)]
	[string]$HttpStartPath,
    [switch]$TestWithCustomInstanceId = $false,
    [switch]$TriggerFault = $false,
    [ValidateRange(1, 65535)]
    [int]$Port = 7071
)

$ErrorActionPreference = "Stop"
$retryCount = 0;
$statusUrl = $null;
$success = $false;
$haveManuallyRestartedHost = $false;
$funcProcess = $null;
$hostUri = "http://localhost:$Port"
$funcLogId = [guid]::NewGuid().ToString("N")
$funcStandardOutput = Join-Path ([IO.Path]::GetTempPath()) "func-$funcLogId.stdout.log"
$funcStandardError = Join-Path ([IO.Path]::GetTempPath()) "func-$funcLogId.stderr.log"

# Get the directory where this script is located (the DotNetIsolated folder)
$scriptDir = $PSScriptRoot
if ([string]::IsNullOrEmpty($scriptDir)) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}
if ([string]::IsNullOrEmpty($scriptDir)) {
    $scriptDir = "./test/SmokeTests/OOProcSmokeTests/DotNetIsolated"
}
Write-Host "Script directory: $scriptDir" -ForegroundColor Cyan

try {
    Do {
        $testIsRunning = $true;

        try {
            # Start the functions host if it's not running already.
            # Give it up to 1 minute to become healthy, including after a platform error.
            if ($null -eq $funcProcess -or $funcProcess.HasExited) {
                if ($null -ne $funcProcess) {
                    $funcProcess.Dispose()
                    $funcProcess = $null
                }
                Write-Host "Starting the Functions host..." -ForegroundColor Yellow
                Write-Host "Working directory: $scriptDir" -ForegroundColor Cyan

                $funcProcess = Start-Process `
                    -FilePath "func" `
                    -ArgumentList "host", "start", "--port", "$Port" `
                    -WorkingDirectory $scriptDir `
                    -RedirectStandardOutput $funcStandardOutput `
                    -RedirectStandardError $funcStandardError `
                    -PassThru

                Write-Host "Waiting for the Functions host to start up..." -ForegroundColor Yellow
                $startupDeadline = [DateTime]::UtcNow.AddMinutes(1)
                $hostReady = $false
                $startupError = "Host did not report a Running state."
                do {
                    if ($funcProcess.HasExited) {
                        throw "Functions host exited with code $($funcProcess.ExitCode). See $funcStandardOutput and $funcStandardError."
                    }
                    try {
                        $hostStatus = Invoke-RestMethod -Uri "$hostUri/admin/host/status" -TimeoutSec 2
                        $hostReady = $hostStatus.state -eq "Running"
                    } catch {
                        $startupError = $_.Exception.Message
                    }
                    if (-not $hostReady) {
                        Start-Sleep -Seconds 1
                    }
                } until ($hostReady -or [DateTime]::UtcNow -ge $startupDeadline)
                if (-not $hostReady) {
                    throw "Functions host did not become ready within one minute: $startupError. See $funcStandardOutput and $funcStandardError."
                }
            }

            # Make sure the Functions runtime is up and running
            $pingUrl = "$hostUri/admin/host/ping"
            Write-Host "Pinging app at $pingUrl to ensure the host is healthy" -ForegroundColor Yellow
            Invoke-RestMethod -Method Post -Uri $pingUrl
            Write-Host "Host is healthy!" -ForegroundColor Green

            # Start orchestrator if it hasn't been started yet
            if ($statusUrl -eq $null){
                $baseUri = "$hostUri/$HttpStartPath"

                $customInstanceId = $null
                if ($TestWithCustomInstanceId) {
                    $customInstanceId = [guid]::NewGuid().ToString()
                    $startOrchestrationUri = $baseUri + "?instanceId=" + $customInstanceId
                } else {
                    $startOrchestrationUri = $baseUri
                }

                Write-Host "Starting a new orchestration instance via POST to $startOrchestrationUri..." -ForegroundColor Yellow

                $result = Invoke-RestMethod -Method Post -Uri $startOrchestrationUri

                # Check that the returned instance ID matches the requested one (if provided)
                if ($TestWithCustomInstanceId -and $customInstanceId -and $result.id -ne $customInstanceId) {
                    throw "Returned instance ID '$($result.id)' does not match requested instance ID '$customInstanceId'"
                }

                Write-Host "Started orchestration with instance ID '$($result.id)'!" -ForegroundColor Yellow
                Write-Host "Waiting for orchestration to complete..." -ForegroundColor Yellow

                $statusUrl = $result.statusQueryGetUri

                if ($TriggerFault) {
                    $startFaultUri = $result.sendEventPostUri.Replace("{eventName}", "StartFault")
                    Write-Host "Triggering the fault after receiving the instance status URL..." -ForegroundColor Yellow
                    Invoke-RestMethod -Method Post -Uri $startFaultUri -ContentType "application/json" -Body "null"
                }

                # sleep for a bit to give the orchestrator a chance to start,
                # then loop once more in case the orchestrator ran quickly, made the host unhealthy,
                # and the functions host needs to be restarted
                Start-Sleep -Seconds 5
                continue;
            }

            # Check the orchestrator status
            $result = Invoke-RestMethod -Method Get -Uri $statusUrl
            $runtimeStatus = $result.runtimeStatus
            Write-Host "Orchestration is $runtimeStatus" -ForegroundColor Yellow
            Write-Host $result

            if ($result.runtimeStatus -eq "Completed") {
                $success = $true
                $testIsRunning = $false
                break
            }
            if ($result.runtimeStatus -eq "Failed") {
                $success = $false
                $testIsRunning = $false
                break
            }

            # If the orchestrator did not complete yet, wait for a bit before checking again
            Start-Sleep -Seconds 2
            $retryCount = $retryCount + 1

        } catch {
            # we expect to enter this 'catch' block if any of our HTTP requests to the host fail.
            # Some failures observed during development include:
            # - The host is not running/was restarting/was killed
            # - The host is running but not healthy (OOMs may cause this), so it needs to be forcibly restarted
            Write-Host "An error occurred:" -ForegroundColor Red
            Write-Host $_ -ForegroundColor Red
            Get-Content $funcStandardOutput -Tail 100 -ErrorAction SilentlyContinue
            Get-Content $funcStandardError -Tail 100 -ErrorAction SilentlyContinue

            # When testing for platform errors, we want to make sure the Functions host is healthy and ready to take requests.
            # The Host can get into bad states (for example, in an OOM-inducing test) where it does not self-heal.
            # For these cases, we manually restart the host to ensure it is in a good state. We only do this once per test.
            if ($haveManuallyRestartedHost -eq $false) {

                # We stop the host process and wait for a bit before checking if it is running again.
                Write-Host "Restarting the Functions host..." -ForegroundColor Yellow
                if ($null -ne $funcProcess -and -not $funcProcess.HasExited) {
                    Stop-Process -Id $funcProcess.Id -Force
                    $funcProcess.WaitForExit()
                }

                Start-Sleep -Seconds 5

                # Log whether the process kill succeeded
                $haveManuallyRestartedHost = $true
                Write-Host "Host process killed: $($funcProcess.HasExited)" -ForegroundColor Yellow

                # the beginning of the loop will restart the host
                continue
            }

            # Rethrow the original exception
            throw
        }

    } while (($testIsRunning -eq $true) -and ($retryCount -lt 65))

    if ($success -eq $false) {
        throw "Orchestration failed or did not compete in time! :("
    }

    Write-Host "Success!" -ForegroundColor Green
} finally {
    if ($null -ne $funcProcess) {
        if (-not $funcProcess.HasExited) {
            Stop-Process -Id $funcProcess.Id -Force
            $funcProcess.WaitForExit()
        }
        $funcProcess.Dispose()
    }
}