using namespace System.Net

param($Request, $TriggerMetadata)

$ErrorActionPreference = "Stop"

# Parameters
$appPlanName = $Request.Body.appPlanName
$resourceGroup = $Request.Body.resourceGroup
$location = $Request.Body.location ?? "centralus"
$subscriptionId = $Request.Body.subscriptionId

$sku = $Request.Body.sku
$workerType = $Request.Body.OSType
$minimumWorkerCount = $Request.Body.minInstanceCount
$maximumWorkerCount = $Request.Body.maxInstanceCount

if (!$appPlanName) {
    Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
        StatusCode = [HttpStatusCode]::BadRequest
        Body = "The request JSON must contain an 'appPlanName' field."
    })
    return
}

"**********CREATE - Connecting to Azure Account***********"
$azurePassword = ConvertTo-SecureString $env:DFTEST_AAD_CLIENT_SECRET -AsPlainText -Force
$psCred = New-Object System.Management.Automation.PSCredential($env:AZURE_APP_ID , $azurePassword)
Connect-AzAccount -Credential $psCred -Tenant $env:AZURE_TENANT_ID -ServicePrincipal
Set-AzContext -SubscriptionId $subscriptionId

Write-Host "New-AzFunctionAppPlan -Location $location -Name $appPlanName -ResourceGroupName $resourceGroup -Sku $sku -WorkerType $workerType -MinimumWorkerCount $minimumWorkerCount -MaximumWorkerCount $maximumWorkerCount"

try {
    New-AzFunctionAppPlan -Location $location -Name $appPlanName -ResourceGroupName $resourceGroup -Sku $sku -WorkerType $workerType -MinimumWorkerCount $minimumWorkerCount -MaximumWorkerCount $maximumWorkerCount

    Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
        StatusCode = [HttpStatusCode]::Created
    })
}
catch {
    Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
        StatusCode = [HttpStatusCode]::BadRequest
    })
}
