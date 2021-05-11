using namespace System.Net

# Input bindings are passed in via param block.
param($Request, $TriggerMetadata)

$appName = $Request.Body.appName
$resourceGroup = $Request.Body.resourceGroup
$testName = $Request.Body.testName

# Connecting to Azure account
$azurePassword = ConvertTo-SecureString $env:DFTEST_AAD_CLIENT_SECRET -AsPlainText -Force
$psCred = New-Object System.Management.Automation.PSCredential($env:AZURE_APP_ID , $azurePassword)
Connect-AzAccount -Credential $psCred -Tenant $env:AZURE_TENANT_ID -ServicePrincipal

# Get function key
$publishingCredentials = Invoke-AzResourceAction  `
     -ResourceGroupName $resourceGroup `
     -ResourceType 'Microsoft.Web/sites/config' `
     -ResourceName ('{0}/publishingcredentials' -f $appName) `
     -Action list `
     -ApiVersion 2019-08-01 `
     -Force

$base64Credentials = [Convert]::ToBase64String(
    [Text.Encoding]::ASCII.GetBytes(
        ('{0}:{1}' -f $publishingCredentials.Properties.PublishingUserName, $publishingCredentials.Properties.PublishingPassword)
    )
)

$jwtToken = Invoke-RestMethod `
     -Uri ('https://{0}.scm.azurewebsites.net/api/functions/admin/token' -f $appName) `
     -Headers @{ Authorization = ('Basic {0}' -f $base64Credentials) }

$functionsKeysResponse = Invoke-RestMethod `
     -Uri ('https://{0}.azurewebsites.net/admin/functions/{1}/keys' -f $appName, $testName) `
     -Headers @{Authorization = ("Bearer {0}" -f $jwtToken) } `
     -Method GET

$functionKey = $functionsKeysResponse.keys.value

# Triggering the test function
$httpPath = $Request.Body.httpPath
$testParameters = $Request.Body.testParameters
$httpApiUrl = "https://${appName}.azurewebsites.net/${httpPath}?${testParameters}code=${functionKey}"

Write-Host "Starting test by sending a POST to $httpApiUrl..."
$httpResponse = Invoke-WebRequest -Method POST "${httpApiUrl}"

# Send back the response content, which is expected to be the management URLs
# of the root orchestrator function
Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
    StatusCode = [HttpStatusCode]::OK
    Body = $httpResponse.Content
})