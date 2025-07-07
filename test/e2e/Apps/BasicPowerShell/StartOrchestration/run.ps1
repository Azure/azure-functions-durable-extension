#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

using namespace System.Net

param($Request, $TriggerMetadata)

$orchestrationName = $Request.Query.orchestrationName
$InstanceId = Start-DurableOrchestration -FunctionName $orchestrationName
Write-Host "Started orchestration with ID = '$InstanceId'"

$Response = New-DurableOrchestrationCheckStatusResponse -Request $Request -InstanceId $InstanceId
Push-OutputBinding -Name Response -Value $Response
