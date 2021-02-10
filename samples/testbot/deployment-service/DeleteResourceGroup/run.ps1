param($Request, $TriggerMetadata)

$appInfo = @'
ResourceGroup = vabachudurablerg5
FunctionName = VabachuDurablePowershellApp5
Location = centralUS
StorageAccount = vabachudurablestorage5
Runtime = PowerShell
SubscriptionID = <sub id>
IdentityType = SystemAssigned
'@

$appInfoHashTable = ConvertFrom-StringData -StringData $appInfo

try {
    #Delete resource group
    Remove-AzResourceGroup -Name $appInfoHashTable.ResourceGroup -Force

    Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
        StatusCode = [HttpStatusCode]::OK
    })
}
catch {
    Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
        StatusCode = [HttpStatusCode]::OK
    })
}
