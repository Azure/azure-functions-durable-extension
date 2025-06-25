using namespace System.Net

param($Request, $TriggerMetadata)

$InstanceId = $Request.Query.id

$status = Get-DurableStatus -InstanceId $InstanceId -ShowHistory -ShowHistoryOutput -ShowInput

if (!$status) {
    Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
        StatusCode = [HttpStatusCode]::NotFound
        Body = "Orchestration metadata not found."
    })
} else {
    Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
        StatusCode = [HttpStatusCode]::OK
        Body = $status
        ContentType = "application/json"
    })
}