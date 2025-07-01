#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

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