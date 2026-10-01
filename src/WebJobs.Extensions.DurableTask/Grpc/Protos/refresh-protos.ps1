#!/usr/bin/env pwsh
param(
    [string]$branch = "main"
)

# Fail with an error if the PowerShell version is less than 7.0
if ($PSVersionTable.PSVersion -lt [Version]"7.0") {
    Write-Error "This script requires PowerShell 7.0 or later."
    exit 1
}

# Resolve once so all downloads use the same upstream commit. Pass a commit SHA
# with -branch to reproduce a specific refresh.
$commitDetails = Invoke-RestMethod -Uri "https://api.github.com/repos/microsoft/durabletask-protobuf/commits/$branch"
$commitId = $commitDetails.sha

# Upstream paths can be nested while the host's imported files remain flat.
$protoFiles = @(
    @{
        SourcePath = "orchestrator_service.proto"
        FileName = "orchestrator_service.proto"
    },
    @{
        SourcePath = "durable-task-scheduler/large_payload_purge.proto"
        FileName = "large_payload_purge.proto"
    }
)

# Download each proto file to the local directory using the above commit ID
foreach ($protoFile in $protoFiles) {
    $url = "https://raw.githubusercontent.com/microsoft/durabletask-protobuf/$commitId/protos/$($protoFile.SourcePath)"
    $outputFile = Join-Path $PSScriptRoot $protoFile.FileName

    try {
        Invoke-WebRequest -Uri $url -OutFile $outputFile        
    }
    catch {
        Write-Error "Failed to download $url to ${outputFile}: $_"
        exit 1
    }

    Write-Output "Downloaded $url to $outputFile"
}

$versionsFile = Join-Path $PSScriptRoot 'versions.txt'
@("# The following files were downloaded from branch $branch at $(Get-Date -Format "yyyy-MM-dd HH:mm:ss" -AsUTC) UTC") +
    @($protoFiles | ForEach-Object {
        "https://raw.githubusercontent.com/microsoft/durabletask-protobuf/$commitId/protos/$($_.SourcePath)"
    }) | Set-Content -Path $versionsFile

Write-Host "Wrote commit ID $commitId to $versionsFile" -ForegroundColor Green