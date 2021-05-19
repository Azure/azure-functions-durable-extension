using namespace System.Net

param($Request, $TriggerMetadata)

$ErrorActionPreference = "Stop"

# Parameters
$appName = $Request.Body.appName
$resourceGroup = $Request.Body.resourceGroup
$storageAccount = $Request.Body.storageAccount
$runtime = $Request.Body.runtime
$subscriptionId = $Request.Body.subscriptionId
$appPlanName = $Request.Body.appPlanName
$functionsVersion = $Request.Body.functionsVersion

if (!$appName) {
    Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
        StatusCode = [HttpStatusCode]::BadRequest
        Body = "The request JSON must contain an 'appName' field."
    })
    return
}

"**********CREATE - Connecting to Azure Account***********"
$azurePassword = ConvertTo-SecureString $env:DFTEST_AAD_CLIENT_SECRET -AsPlainText -Force
$psCred = New-Object System.Management.Automation.PSCredential($env:AZURE_APP_ID , $azurePassword)
Connect-AzAccount -Credential $psCred -Tenant $env:AZURE_TENANT_ID -ServicePrincipal
Set-AzContext -SubscriptionId $subscriptionId

try {
    $originalFunctionsVersion = $functionsVersion

    if ($originalFunctionsVersion -eq "1"){
        $functionsVersion = "2"
    }

    $createNewFunctionAppWithPlanCommand = "New-AzFunctionApp -Name $appName -PlanName $appPlanName -ResourceGroupName $resourceGroup -StorageAccount $storageAccount -Runtime $runtime -SubscriptionId $subscriptionId -FunctionsVersion $functionsVersion"
    Write-Host $createNewFunctionAppWithPlanCommand
    Invoke-Expression $createNewFunctionAppWithPlanCommand

    if ($originalFunctionsVersion -eq "1"){
        $v1setting = @{"FUNCTIONS_EXTENSION_VERSION"="~1"}
        Write-Host "Update-AzFunctionAppSetting -Name $appName -ResourceGroupName $resourceGroup -AppSetting $v1setting -Force"
        Update-AzFunctionAppSetting -Name $appName -ResourceGroupName $resourceGroup -AppSetting $v1setting -Force
    }
    
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