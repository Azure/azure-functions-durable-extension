# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

param(
    [string]$pw="$env:SA_PASSWORD",
    [string]$sqlpid="Express",
    [string]$tag="2019-latest",
    [int]$port=1433,
    [string]$dbname="DurableDB",
    [string]$collation="Latin1_General_100_BIN2_UTF8",
    [string]$additinalRunFlags=""
)

Write-Host "Pulling down the mcr.microsoft.com/mssql/server:$tag image..."
docker pull mcr.microsoft.com/mssql/server:$tag

# Start the SQL Server docker container with the specified edition
Write-Host "Starting SQL Server $tag $sqlpid docker container on port $port" -ForegroundColor DarkYellow
docker run $additinalRunFlags --name mssql-server -e 'ACCEPT_EULA=Y' -e "MSSQL_SA_PASSWORD=$pw" -e "MSSQL_PID=$sqlpid" -p ${port}:1433 -d mcr.microsoft.com/mssql/server:$tag

# The container needs a bit more time before it can start accepting commands
Write-Host "Sleeping for 30 seconds to let the container finish initializing..." -ForegroundColor Yellow
Start-Sleep -Seconds 30

# Check to see what containers are running
docker ps

# Create the database with strict binary collation
Write-Host "Creating '$dbname' database with '$collation' collation" -ForegroundColor DarkYellow
docker exec -d mssql-server /opt/mssql-tools/bin/sqlcmd -S . -U sa -P "$pw" -Q "CREATE DATABASE [$dbname] COLLATE $collation"