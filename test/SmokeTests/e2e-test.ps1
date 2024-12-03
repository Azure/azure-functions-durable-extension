
param(
	[Parameter(Mandatory=$true)]
	[string]$DockerfilePath,
	[Parameter(Mandatory=$true)]
	[string]$HttpStartPath,
	[string]$ImageName="dfapp",
	[string]$ContainerName="app",
	[switch]$NoSetup=$false,
	[switch]$NoValidation=$false,
	[int]$Sleep=30,
  	[switch]$SetupSQLServer=$false,
  	[string]$pw="$env:SA_PASSWORD",
    	[string]$sqlpid="Express",
     	[string]$tag="2019-latest",
    	[int]$port=1433,
     	[string]$dbname="DurableDB",
    	[string]$collation="Latin1_General_100_BIN2_UTF8"
)

function Exit-OnError() {
	# There appears to be a known problem in GitHub Action's `pwsh` shell preventing it from failing fast on an error:
	# https://github.com/actions/runner-images/issues/6668#issuecomment-1364540817
	# Therefore, we manually check if there was an error an fail if so.
	if (!$LASTEXITCODE.Equals(0)) {exit $LASTEXITCODE}
}

$ErrorActionPreference = "Stop"
$AzuriteVersion = "3.32.0"

if ($NoSetup -eq $false) {
	# Build the docker image first, since that's the most critical step
	Write-Host "Building sample app Docker container from '$DockerfilePath'..." -ForegroundColor Yellow
	docker build --pull -f $DockerfilePath -t $ImageName --progress plain $PSScriptRoot/../../
	Exit-OnError

	# Next, download and start the Azurite emulator Docker image
	Write-Host "Pulling down the mcr.microsoft.com/azure-storage/azurite:$AzuriteVersion image..." -ForegroundColor Yellow
	docker pull "mcr.microsoft.com/azure-storage/azurite:${AzuriteVersion}"
	Exit-OnError

	Write-Host "Starting Azurite storage emulator using default ports..." -ForegroundColor Yellow
	docker run --name 'azurite' -p 10000:10000 -p 10001:10001 -p 10002:10002 -d "mcr.microsoft.com/azure-storage/azurite:${AzuriteVersion}"
	Exit-OnError

 	if ($SetupSQLServer -eq $true) {
		Write-Host "Pulling down the mcr.microsoft.com/mssql/server:$tag image..."
		docker pull mcr.microsoft.com/mssql/server:$tag
		Exit-OnError

		# Start the SQL Server docker container with the specified edition
		Write-Host "Starting SQL Server $tag $sqlpid docker container on port $port" -ForegroundColor DarkYellow
		docker run --name mssql-server -e 'ACCEPT_EULA=Y' -e "MSSQL_SA_PASSWORD=$pw" -e "MSSQL_PID=$sqlpid" -p ${port}:1433 -d mcr.microsoft.com/mssql/server:$tag
		Exit-OnError

		# Wait for SQL Server to be ready
		Write-Host "Waiting for SQL Server to be ready..." -ForegroundColor Yellow
		Start-Sleep -Seconds 30  # Adjust the sleep duration based on your SQL Server container startup time
		Exit-OnError

		Write-Host "Checking if SQL Server is still running..." -ForegroundColor Yellow
		$sqlServerStatus = docker inspect -f '{{.State.Status}}' mssql-server
		Exit-OnError

		if ($sqlServerStatus -ne "running") {
			Write-Host "Unexpected SQL Server status: $sqlServerStatus" -ForegroundColor Yellow
			docker logs mssql-server
			exit 1;
		}

 		# Get SQL Server IP Address - used to create SQLDB_Connection
		Write-Host "Getting IP Address..." -ForegroundColor Yellow
	 	$serverIpAddress = docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' mssql-server
		Exit-OnError

	 	# Create the database with strict binary collation
		Write-Host "Creating '$dbname' database with '$collation' collation" -ForegroundColor DarkYellow
		docker exec -d mssql-server /opt/mssql-tools18/bin/sqlcmd -S . -U sa -P "$pw" -Q "CREATE DATABASE [$dbname] COLLATE $collation"
		Exit-OnError

  		# Wait for database to be ready
		Write-Host "Waiting for database to be ready..." -ForegroundColor Yellow
		Start-Sleep -Seconds 30  # Adjust the sleep duration based on your database container startup time
		Exit-OnError

  		# Finally, start up the application container, connecting to the SQL Server container
		Write-Host "Starting the $ContainerName application container" -ForegroundColor Yellow
	 	docker run --name $ContainerName -p 8080:80 -it --add-host=host.docker.internal:host-gateway -d `
			--env "SQLDB_Connection=Server=$serverIpAddress,1433;Database=$dbname;User=sa;Password=$pw;" `
			--env 'AzureWebJobsStorage=UseDevelopmentStorage=true;DevelopmentStorageProxyUri=http://host.docker.internal' `
			--env 'WEBSITE_HOSTNAME=localhost:8080' `
			$ImageName
		Exit-OnError
   	}
    	else {
		Write-Host "Starting $ContainerName application container" -ForegroundColor Yellow
		docker run --name $ContainerName -p 8080:80 -it --add-host=host.docker.internal:host-gateway -d `
			--env 'AzureWebJobsStorage=UseDevelopmentStorage=true;DevelopmentStorageProxyUri=http://host.docker.internal' `
			--env 'WEBSITE_HOSTNAME=localhost:8080' `
			$ImageName
     	}
		Exit-OnError
}

if ($sleep -gt  0) {
	# The container needs a bit more time before it can start receiving requests
	Write-Host "Sleeping for $Sleep seconds to let the container finish initializing..." -ForegroundColor Yellow
	Start-Sleep -Seconds $Sleep
}

# Check to see what containers are running
docker ps
Exit-OnError

try {
	# Make sure the Functions runtime is up and running
	$pingUrl = "http://localhost:8080/admin/host/ping"
	Write-Host "Pinging app at $pingUrl to ensure the host is healthy" -ForegroundColor Yellow
	Invoke-RestMethod -Method Post -Uri "http://localhost:8080/admin/host/ping"
	Exit-OnError

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
