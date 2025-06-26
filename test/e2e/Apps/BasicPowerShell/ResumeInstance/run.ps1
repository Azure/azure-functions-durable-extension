using namespace System.Net

param($Request, $TriggerMetadata)

$instanceId = $Request.Query.instanceId

try {
    Resume-DurableOrchestration -InstanceId $instanceId -Reason "Resuming the instance for test."
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