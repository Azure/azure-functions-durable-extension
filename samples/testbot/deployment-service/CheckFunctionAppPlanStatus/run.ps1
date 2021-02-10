using namespace System.Net

param($Request, $TriggerMetadata)

$ErrorActionPreference = "Stop"

$resourceGroupName = $Request.Body.resourceGroup
$appPlanName = $Request.Body.appPlanName
$subscriptionId = $Request.Body.subscriptionId
Set-AzContext -SubscriptionId $subscriptionId

try {
    Get-AzFunctionAppPlan -Name $appPlanName -ResourceGroupName $resourceGroupName
    Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
        StatusCode = [HttpStatusCode]::OK
    })
}
catch {
    Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
        StatusCode = [HttpStatusCode]::NotFound
    })
}