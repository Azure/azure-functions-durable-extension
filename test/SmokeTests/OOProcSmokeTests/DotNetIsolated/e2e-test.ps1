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
docker run -p 10000:10000 -p 10001:10001 -p 10002:10002 -d mcr.microsoft.com/azure-storage/azurite

# Finally, start up the smoke test container, which will connect to the Azurite container
docker run -p 8080:80 -it --add-host=host.docker.internal:host-gateway -d `
	--env 'AzureWebJobsStorage=UseDevelopmentStorage=true;DevelopmentStorageProxyUri=http://host.docker.internal' `
	--env 'WEBSITE_HOSTNAME=localhost:8080' `
	$imageName

# The container needs a bit more time before it can start receiving requests
Write-Host "Sleeping for 60 seconds to let the container finish initializing..." -ForegroundColor Yellow
Start-Sleep -Seconds 60

# Check to see what containers are running
docker ps

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

if ($success -eq $false) {
	throw "Orchestration didn't complete in time! :("
}

Write-Host "Success!" -ForegroundColor Green