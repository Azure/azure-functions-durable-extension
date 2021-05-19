using namespace System.Net

param($Request, $TriggerMetadata)

$ErrorActionPreference = "Stop"

$resourceGroupName = $Request.Body.resourceGroup
$storageAccountName = $Request.Body.storageAccount
$Location = "centralUS"
$subscriptionId = $Request.Body.subscriptionId

$azurePassword = ConvertTo-SecureString $env:DFTEST_AAD_CLIENT_SECRET -AsPlainText -Force
$psCred = New-Object System.Management.Automation.PSCredential($env:AZURE_APP_ID , $azurePassword)
Connect-AzAccount -Credential $psCred -Tenant $env:AZURE_TENANT_ID -ServicePrincipal
Set-AzContext -SubscriptionId $subscriptionId

try {
    #Create new storage account
    New-AzStorageAccount -ResourceGroupName $resourceGroupName -AccountName $storageAccountName -Location $Location -SkuName Standard_GRS

    Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
        StatusCode = [HttpStatusCode]::Created
    })
}
catch {
    Write-Host $_
    Write-Host $_.ScriptStackTrace
    Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
        StatusCode = [HttpStatusCode]::BadRequest
    })
}