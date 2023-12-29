param(
    [Parameter(Mandatory=$true)]
    [string]$DockerfilePath,
    [Parameter(Mandatory=$true)]
    [string]$HttpStartPath,
    [string]$ImageName="dfapp",
    [string]$ContainerName="app",
    [string]$SqlServerContainerName="sql-server-container",
    [string]$SqlServerPassword="YourStrongPassword",
    [switch]$NoSetup=$false,
    [switch]$NoValidation=$false,
    [int]$Sleep=30
)

$ErrorActionPreference = "Stop"

if ($NoSetup -eq $false) {
    # Build the Docker image first, since that's the most critical step
    Write-Host "Building sample app Docker container from '$DockerfilePath'..." -ForegroundColor Yellow
    docker build -f $DockerfilePath -t $ImageName --progress plain $PSScriptRoot/../../

    # Start the SQL Server container
    Write-Host "Starting SQL Server container..." -ForegroundColor Yellow
    docker run -e 'ACCEPT_EULA=Y' -e "SA_PASSWORD=$SqlServerPassword" --name $SqlServerContainerName -p 1433:1433 -d mcr.microsoft.com/mssql/server

    # Wait for SQL Server to be ready
    Write-Host "Waiting for SQL Server to be ready..." -ForegroundColor Yellow
    Start-Sleep -Seconds 30  # Adjust the sleep duration based on your SQL Server container startup time

    # Finally, start up the application container, connecting to the SQL Server container
    docker run --name $ContainerName -p 8080:80 -it --add-host=host.docker.internal:host-gateway -d `
        --env 'SqlConnectionString=Server=$SqlServerContainerName,1433;Database=YourDatabase;User=sa;Password=$SqlServerPassword;' `
        --env 'AzureWebJobsStorage=UseDevelopmentStorage=true;DevelopmentStorageProxyUri=http://host.docker.internal' `
        --env 'WEBSITE_HOSTNAME=localhost:8080' `
        $ImageName

    # The container needs a bit more time before it can start accepting commands
    Write-Host "Sleeping for 30 seconds to let the container finish initializing..." -ForegroundColor Yellow
    Start-Sleep -Seconds 30
    
    # Check to see what containers are running
    docker ps
    
    # Create the database with strict binary collation
    Write-Host "Creating '$dbname' database with '$collation' collation" -ForegroundColor DarkYellow
    docker exec -d mssql-server /opt/mssql-tools/bin/sqlcmd -S . -U sa -P "$pw" -Q "CREATE DATABASE [$dbname] COLLATE $collation"
}

# Check to see what containers are running
docker ps

try {
  	# Make sure the Functions runtime is up and running
  	$pingUrl = "http://localhost:8080/admin/host/ping"
  	Write-Host "Pinging app at $pingUrl to ensure the host is healthy" -ForegroundColor Yellow
  	Invoke-RestMethod -Method Post -Uri "http://localhost:8080/admin/host/ping"

  	if ($NoValidation -eq $false) {
    		# Note that any HTTP protocol errors (e.g. HTTP 4xx or 5xx) will cause an immediate failure
    		$startOrchestrationUri = "http://localhost:8080/$HttpStartPath"
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
  	}
  
    if ($success -eq $false) {
        throw "Orchestration didn't complete in time! :("
    }
  } catch {
    	Write-Host "An error occurred:" -ForegroundColor Red
    	Write-Host $_ -ForegroundColor Red
  
    	# Dump the docker logs to make debugging the issue easier
    	Write-Host "Below are the docker logs for the app container:" -ForegroundColor Red
    	docker logs $ContainerName
    
    	# Rethrow the original exception
    	throw
}

Write-Host "Success!" -ForegroundColor Green
