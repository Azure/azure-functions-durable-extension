# Installing PowerShell: https://docs.microsoft.com/powershell/scripting/install/installing-powershell

param(
    [string]$imageName="dfapp-dotnet-isolated"
)

$ErrorActionPreference = "Stop"

# Build the docker image first, since that's the most critical step
Write-Host "Building sample app Docker container..." -ForegroundColor Yellow
docker build -f $PSScriptRoot/Dockerfile -t $imageName $PSScriptRoot/../../../../

# Next, download and start the Azurite emulator Docker image
Write-Host "Pulling down the mcr.microsoft.com/azure-storage/azurite image..." -ForegroundColor Yellow
docker pull mcr.microsoft.com/azure-storage/azurite

Write-Host "Starting Azurite storage emulator using default ports..." -ForegroundColor Yellow
docker run --name 'azurite' -p 10000:10000 -p 10001:10001 -p 10002:10002 -d mcr.microsoft.com/azure-storage/azurite

# Finally, start up the smoke test container, which will connect to the Azurite container
docker run --name 'app' -p 8080:80 -it --add-host=host.docker.internal:host-gateway -d `
	--env 'AzureWebJobsStorage=UseDevelopmentStorage=true;DevelopmentStorageProxyUri=http://host.docker.internal' `
	--env 'WEBSITE_HOSTNAME=localhost:8080' `
	$imageName

# The container needs a bit more time before it can start receiving requests
Write-Host "Sleeping for 30 seconds to let the container finish initializing..." -ForegroundColor Yellow
Start-Sleep -Seconds 30

# Check to see what containers are running
docker ps

try {
	# Note that any HTTP protocol errors (e.g. HTTP 4xx or 5xx) will cause an immediate failure
	$startOrchestrationUri = "http://localhost:8080/api/StartHelloCitiesTyped"
	Write-Host "Starting a new orchestration instance via POST to $startOrchestrationUri..." -ForegroundColor Yellow

	$result = Invoke-RestMethod -Method Post -Uri $startOrchestrationUri
	Write-Host "Started orchestration with instance ID '$($result.id)'!" -ForegroundColor Yellow
	Write-Host "Waiting for orchestration to complete..." -ForegroundColor Yellow

	$retryCount = 0
	$success = $false
	$statusUrl = $result.statusQueryGetUri

	while ($retryCount -lt 15) {
		$result = Invoke-RestMethod -Method Get -Uri $statusUrl
		$runtimeStatus = $result.runtimeStatus
		Write-Host "Orchestration is $runtimeStatus" -ForegroundColor Yellow

		if ($result.runtimeStatus -eq "Completed") {
			$success = $true
			break
		}

		Start-Sleep -Seconds 1
		$retryCount = $retryCount + 1
	}
} catch {
	Write-Host "An error occurred:" -ForegroundColor Red
	Write-Host $_ -ForegroundColor Red

	# Dump the docker logs to make debugging the issue easier
	Write-Host "Below are the docker logs for the app container:" -ForegroundColor Red
	docker logs 'app'

	# Rethrow the original exception
	throw
}

if ($success -eq $false) {
	throw "Orchestration didn't complete in time! :("
}

Write-Host "Success!" -ForegroundColor Green