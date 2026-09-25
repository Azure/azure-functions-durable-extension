[CmdletBinding(DefaultParameterSetName = 'Prepare')]
param(
    [Parameter(Mandatory = $true)][uri] $BaseUri,
    [Parameter(Mandatory = $true)][string] $ArtifactsDirectory,
    [Parameter(Mandatory = $true, ParameterSetName = 'Purge')][string] $InstanceId,
    [Parameter(Mandatory = $true, ParameterSetName = 'Purge')][string[]] $ReferencedBlobNames
)

$ErrorActionPreference = 'Stop'
if (!$BaseUri.IsLoopback) { throw 'This test must target an isolated loopback Functions host.' }
New-Item -ItemType Directory -Path $ArtifactsDirectory -Force | Out-Null

function Get-Api([string] $Path) {
    Invoke-RestMethod -Uri ([uri]::new($BaseUri, $Path)) -TimeoutSec 20
}

function Post-Api([string] $Path) {
    Invoke-RestMethod -Method Post -Uri ([uri]::new($BaseUri, $Path)) -TimeoutSec 90
}

function Save-Evidence([string] $Name, $Value) {
    $Value | ConvertTo-Json -Depth 30 | Set-Content (Join-Path $ArtifactsDirectory "$Name.json")
}

$functions = Get-Api '/admin/functions'
$expected = @('BlobPurgeJobOrchestrator', 'GetLargePayloadTombstonesActivity', 'DeleteExternalBlobActivity', 'ReportLargePayloadPurgeResultsActivity')
foreach ($name in $expected) {
    $function = @($functions | Where-Object name -eq $name)
    if ($function.Count -ne 1) { throw "Expected one indexed SDK function named $name" }
    if ($function[0].config.scriptFile -notlike '*Microsoft.Azure.Functions.Worker.Extensions.DurableTask.AzureBlobPayloads.dll') {
        throw "SDK function $name did not come from the SDK package assembly."
    }
}
Save-Evidence 'indexed-functions' $functions
$before = Get-Api '/api/large-payload-purge/storage'
Save-Evidence 'storage-before' $before
$enabled = Post-Api '/api/large-payload-purge/true'
Save-Evidence 'enabled' $enabled
$created = if ($PSCmdlet.ParameterSetName -eq 'Prepare') {
    Post-Api '/api/large-payload-purge/payload'
} else {
    @{ instanceId = $InstanceId }
}
Save-Evidence 'created-instance' $created
$deadline = [DateTime]::UtcNow.AddSeconds(90)
do {
    $state = Get-Api "/api/large-payload-purge/instances/$($created.instanceId)"
    if ($state.status -eq 'Failed') { throw "Payload orchestration failed: $($state.failure)" }
    if ($state.status -eq 'Completed') { break }
    Start-Sleep -Milliseconds 500
} while ([DateTime]::UtcNow -lt $deadline)
if ($state.status -ne 'Completed') { throw 'Payload orchestration did not complete.' }
Save-Evidence 'completed-instance' $state
$stored = Get-Api '/api/large-payload-purge/storage'
Save-Evidence 'storage-before-purge' $stored
if ($PSCmdlet.ParameterSetName -eq 'Prepare') {
    $newBlobNames = @($stored.blobs | Where-Object { $_.name -notin @($before.blobs.name) } | ForEach-Object name)
    if ($newBlobNames.Count -eq 0) { throw 'No actual externalized payload blobs were created.' }
    Write-Output "Prepared instance $($created.instanceId), worker PID $($created.workerPid). Observe its backend v2 references before running the Purge parameter set."
    return
}

if ($ReferencedBlobNames.Count -eq 0) { throw 'The backend reference observation must identify at least one physical blob.' }
foreach ($name in $ReferencedBlobNames) {
    if ($name -notin @($stored.blobs.name)) { throw "Referenced payload blob $name is absent before purge." }
}
Save-Evidence 'referenced-blob-names' $ReferencedBlobNames

$purged = Post-Api "/api/large-payload-purge/instances/$($created.instanceId)/purge"
Save-Evidence 'purged-instance' $purged
$deadline = [DateTime]::UtcNow.AddSeconds(180)
do {
    $storage = Get-Api '/api/large-payload-purge/storage'
    $remaining = @($storage.blobs | Where-Object { $_.name -in $ReferencedBlobNames })
    if ($remaining.Count -eq 0) { break }
    Start-Sleep -Seconds 2
} while ([DateTime]::UtcNow -lt $deadline)
if ($remaining.Count -ne 0) { throw "SDK cleanup left $($remaining.Count) payload blobs." }
Save-Evidence 'storage-after-cleanup' $storage
Save-Evidence 'override-disabled' (Post-Api '/api/large-payload-purge/override/false')
Save-Evidence 'disabled' (Post-Api '/api/large-payload-purge/false')
Write-Output "Indexed four SDK functions; worker PID $($enabled.workerPid); removed all $($ReferencedBlobNames.Count) observed referenced payload blobs. Other container blobs are not cleanup claims."
