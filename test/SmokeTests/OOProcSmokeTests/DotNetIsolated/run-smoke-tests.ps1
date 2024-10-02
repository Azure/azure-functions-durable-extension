# This is a simple test runner to validate the .NET isolated smoke tests.
# It supercedes the usual e2e-tests.ps1 script for the .NET isolated scenario because building the snmoke test app
# on the docker image is unreliable. For more details, see: https://github.com/Azure/azure-functions-host/issues/7995

# This script is designed specifically to test cases where the isolated worker process experiences a platform failure:
# timeouts, OOMs, etc. For that reason, it is careful to check that the Functions Host is running and healthy at regular
# intervals. This makes these tests run more slowly than other test categories.

param(
	[Parameter(Mandatory=$true)]
	[string]$HttpStartPath
)

$retryCount = 0;
$statusUrl = $null;
$success = $false;
$haveManuallyRestartedHost = $false;

Do {
    $testIsRunning = $true;

    # Start the functions host if it's not running already.
    # Then give it up to 1 minute to start up. 
    # This is a long wait, but from experience the CI can be slow to start up the host, especially after a platform-error.
    $isFunctionsHostRunning = (Get-Process -Name func -ErrorAction SilentlyContinue)
    if ($isFunctionsHostRunning -eq $null) {
        Write-Host "Starting the Functions host..." -ForegroundColor Yellow

        # The '&' operator is used to run the command in the background
        cd ./test/SmokeTests/OOProcSmokeTests/DotNetIsolated && func host start --port 7071 &       
        Write-Host "Waiting for the Functions host to start up..." -ForegroundColor Yellow
        Start-Sleep -Seconds 60
    }

    
    try {
        # Make sure the Functions runtime is up and running
        $pingUrl = "http://localhost:7071/admin/host/ping"
        Write-Host "Pinging app at $pingUrl to ensure the host is healthy" -ForegroundColor Yellow
        Invoke-RestMethod -Method Post -Uri "http://localhost:7071/admin/host/ping"
        Write-Host "Host is healthy!" -ForegroundColor Green

        # Start orchestrator if it hasn't been started yet
        if ($statusUrl -eq $null){
            $startOrchestrationUri = "http://localhost:7071/$HttpStartPath"
            Write-Host "Starting a new orchestration instance via POST to $startOrchestrationUri..." -ForegroundColor Yellow

            $result = Invoke-RestMethod -Method Post -Uri $startOrchestrationUri
            Write-Host "Started orchestration with instance ID '$($result.id)'!" -ForegroundColor Yellow
            Write-Host "Waiting for orchestration to complete..." -ForegroundColor Yellow

            $statusUrl = $result.statusQueryGetUri
            
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

        # When testing for platform errors, we want to make sure the Functions host is healthy and ready to take requests.
        # The Host can get into bad states (for example, in an OOM-inducing test) where it does not self-heal.
        # For these cases, we manually restart the host to ensure it is in a good state. We only do this once per test.
        if ($haveManuallyRestartedHost -eq $false) {
            
            # We stop the host process and wait for a bit before checking if it is running again.
            Write-Host "Restarting the Functions host..." -ForegroundColor Yellow
            Stop-Process -Name "func" -Force
            Start-Sleep -Seconds 5
            
            # Log whether the process kill succeeded
            $haveManuallyRestartedHost = $true
            $isFunctionsHostRunning = ((Get-Process -Name func -ErrorAction SilentlyContinue) -eq $null)
            Write-Host "Host process killed: $isFunctionsHostRunning" -ForegroundColor Yellow
  
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