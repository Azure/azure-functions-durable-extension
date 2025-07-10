#!/usr/bin/env pwsh
param(
    [string]$branch = "main"
)

# Fail with an error if the PowerShell version is less than 7.0
if ($PSVersionTable.PSVersion -lt [Version]"7.0") {
    Write-Error "This script requires PowerShell 7.0 or later."
    exit 1
}

# Get the commit ID of the latest commit in the durabletask-protobuf repository.
# We need this to download the proto files from the correct commit, avoiding race conditions
# in rare cases where the proto files are updated between the time we download the commit ID
# and the time we download the proto files.
$commitDetails = Invoke-RestMethod -Uri "https://api.github.com/repos/microsoft/durabletask-protobuf/commits/$branch"
$commitId = $commitDetails.sha

# These are the proto files we need to download from the durabletask-protobuf repository.
$protoFileNames = @(
    "orchestrator_service.proto"
)

# Download each proto file to the local directory using the above commit ID
foreach ($protoFileName in $protoFileNames) {
    $url = "https://raw.githubusercontent.com/microsoft/durabletask-protobuf/$commitId/protos/$protoFileName"
    $outputFile = "$PSScriptRoot\$protoFileName"

    try {
        Invoke-WebRequest -Uri $url -OutFile $outputFile        
    }
    catch {
        Write-Error "Failed to download $url to ${outputFile}: $_"
        exit 1
    }

    Write-Output "Downloaded $url to $outputFile"
}

# Log the commit ID and the URLs of the downloaded proto files to a versions file.
# Overwrite the file if it already exists.
$versionsFile = "$PSScriptRoot\versions.txt"
Remove-Item -Path $versionsFile -ErrorAction SilentlyContinue

Add-Content `
    -Path $versionsFile `
    -Value "# The following files were downloaded from branch $branch at $(Get-Date -Format "yyyy-MM-dd HH:mm:ss" -AsUTC) UTC"

foreach ($protoFileName in $protoFileNames) {
    Add-Content `
        -Path $versionsFile `
        -Value "https://raw.githubusercontent.com/microsoft/durabletask-protobuf/$commitId/protos/$protoFileName"
}

Write-Host "Wrote commit ID $commitId to $versionsFile" -ForegroundColor Green