using namespace System.Net

param($Request, $TriggerMetadata)

$ErrorActionPreference = "Stop"

$resourceGroupName = $Request.Body.resourceGroup
$subscriptionId = $Request.Body.subscriptionId
Set-AzContext -SubscriptionId $subscriptionId

try
{
    Get-AzResourceGroup -Name $resourceGroupName
    Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
        StatusCode = [HttpStatusCode]::OK
    })
}
catch
{
    Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
        StatusCode = [HttpStatusCode]::NotFound
    })
}