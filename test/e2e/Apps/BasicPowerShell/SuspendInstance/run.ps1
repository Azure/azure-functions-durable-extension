using namespace System.Net

param($Request, $TriggerMetadata)

$instanceId = $Request.Query.instanceId

try {
    Suspend-DurableOrchestration -InstanceId $instanceId -Reason "Suspending the instance for test."
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