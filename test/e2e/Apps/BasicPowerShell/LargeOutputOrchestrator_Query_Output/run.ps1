#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

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