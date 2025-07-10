#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

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