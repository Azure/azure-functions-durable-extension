using namespace System.Net

param($Request, $TriggerMetadata)

$instanceId = $Request.Query.instanceId

try {
    Stop-DurableOrchestration -InstanceId $instanceId -Reason "Long-running orchestration was terminated early."
    Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
        StatusCode = [HttpStatusCode]::OK
        Body = ""
    })
}
catch {
    Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
        StatusCode = [HttpStatusCode]::BadRequest
        Body = $_.Exception.Message
        ContentType = "text/plain"
    })
}