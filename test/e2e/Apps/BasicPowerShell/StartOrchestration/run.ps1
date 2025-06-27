using namespace System.Net

param($Request, $TriggerMetadata)

$orchestrationName = $Request.Query.orchestrationName
$InstanceId = Start-DurableOrchestration -FunctionName $orchestrationName
Write-Host "Started orchestration with ID = '$InstanceId'"

$Response = New-DurableOrchestrationCheckStatusResponse -Request $Request -InstanceId $InstanceId
Push-OutputBinding -Name Response -Value $Response
