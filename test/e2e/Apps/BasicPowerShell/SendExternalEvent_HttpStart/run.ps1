#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

using namespace System.Net

param($Request, $TriggerMetadata)
Write-Host $Request.Body
$instanceId = $Request.Body | ConvertFrom-Json

$response = @{
    StatusCode = [HttpStatusCode]::OK
    Body = ""
}

try {
    Send-DurableExternalEvent -InstanceId $instanceId -EventName "Approval"
    $response.Body = "External event sent to $instanceId."
}
catch {
    if ($_.Exception.GetType().Name -eq "HttpResponseException") {
        $response.StatusCode = [HttpStatusCode]::BadRequest
        $response.Body = "HttpResponseException: - $($_.Exception.Message)"
    } else {
        $response.StatusCode = [HttpStatusCode]::BadRequest
        $response.Body = "Error: $($_.Exception.GetType().Name) - $($_.Exception.Message)"
    }
}

Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
    StatusCode = $response.StatusCode
    Body = $response.Body
})