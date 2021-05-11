using namespace System.Net

param($Request, $TriggerMetadata)

$ErrorActionPreference = "Stop"

$GitHubOrg = "Azure"
$ProjectName = "azure-functions-durable-extension"
$ProjectFileDir = $Request.Body.projectFileDirPath
$Framework = $Request.Body.framework


# Delete the target directory if it already exists
$srcDir = "$env:TEMP\$ProjectName"
if (Test-Path -Path $srcDir) {
    # Also, make sure we're not currently inside that directory from a previous run
    Set-Location $env:TEMP
    Write-Host "Deleting existing directory $srcDir..."
    Remove-Item $srcDir -Recurse -Force
}

# ************Download from GitHub and create a zip file*********

# Clone the project into the %TEMP% directory
$branchName = $Request.Body.branchName # e.g. "cgillum/perf-testing"
$repoUrl = "https://github.com/$GitHubOrg/$ProjectName"
Write-Host "Cloning branch '$branchName' of $repoUrl into $srcDir..."

# NOTE: git.exe spews out errors to stderr, which confuses PowerShell. We supress all errors from interfering with this script.
$ErrorActionPreference = "Continue"
$result = git clone $repoUrl -b $branchName $srcDir 2>&1
if ($LASTEXITCODE) {
    Throw "git failed (exit code: $LASTEXITCODE):`n$($result -join "`n")"
}
$result | ForEach-Object ToString

# Build the project using the "publish" command so we can get the output for publishing
$buildDir = "$srcDir\$ProjectFileDir"
Set-Location $buildDir
Write-Host "Building $buildDir..."
dotnet publish -p:DeployTarget=Package -f:$Framework                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            
$ErrorActionPreference = "Stop"

# Move out of the current directory or the handle will be held on it
Set-Location $env:TEMP

# Create the zip file for publishing
$targetZipFilePath = "$env:TEMP\app.zip"
if (Test-Path -Path $targetZipFilePath) {
    Write-Host "Deleting existing $targetZipFilePath..."
    Remove-Item $targetZipFilePath -ErrorAction Ignore -Force
}

$zipSrc = "$buildDir\bin\Debug\$Framework\publish\"
Write-Host "Zipping $zipSrc into $targetZipFilePath..."
[System.IO.Compression.ZipFile]::CreateFromDirectory($zipSrc, $targetZipFilePath)
Write-Host "$targetZipFilePath created successfully!"

#"**********DEPLOY - Connecting to Azure Account***********"
$azurePassword = ConvertTo-SecureString $env:DFTEST_AAD_CLIENT_SECRET -AsPlainText -Force
$psCred = New-Object System.Management.Automation.PSCredential($env:AZURE_APP_ID , $azurePassword)
Connect-AzAccount -Credential $psCred -Tenant $env:AZURE_TENANT_ID -ServicePrincipal

$subscriptionId = $Request.Body.subscriptionId
Write-Host "sub id - $subscriptionId"
Set-AzContext -SubscriptionId $subscriptionId

#"**********Deploying a zip file to Function***********"
try
{
    $appName = $Request.Body.appName
    $resourceGroup = $Request.Body.resourceGroup
    Write-Host "Publishing $targetZipFilePath to $appName in resource group $resourceGroup..."
    Write-Host "Publish-AzWebApp -ResourceGroupName $resourceGroup -Name $appName -DefaultProfile $defaultProfile -ArchivePath $targetZipFilePath -Force"
    Publish-AzWebApp -ResourceGroupName $resourceGroup -Name $appName -DefaultProfile $defaultProfile -ArchivePath $targetZipFilePath -Force
    
    Write-Host "Deployed code to function app"
    
    Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
        StatusCode = [HttpStatusCode]::OK
    })
}
catch [Exception]
{
    Write-Host $_.Exception.Message
    Write-Host $_.Exception.InnerException

    Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
        StatusCode = [HttpStatusCode]::BadRequest
    })
}

