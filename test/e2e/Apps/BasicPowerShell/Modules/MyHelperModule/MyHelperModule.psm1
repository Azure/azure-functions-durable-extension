#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

function GetExecutionCount($InstanceId) {
    # Define the file path
    $filePath = "GlobalRetryCount.json"

    # Check if the file exists
    if (-not (Test-Path -Path $filePath)) {
        return 0
    }

    # Read the file and convert its content from JSON
    $jsonData = Get-Content -Path $filePath -Raw | ConvertFrom-Json

    # Output the parsed JSON object
    if ($null -eq $jsonData.$InstanceId) {
        # If the InstanceId does not exist, return 0
        return 0
    }
    return $jsonData.$InstanceId
}

function IncrementExecutionCount($InstanceId) {
    # Define the file path
    $filePath = "GlobalRetryCount.json"

    # Check if the file exists
    if (-not (Test-Path -Path $filePath)) {
        $jsonData = @{}
    }
    else {
        # Read the file and convert its content from JSON
        $jsonData = Get-Content -Path $filePath -Raw | ConvertFrom-Json
    }

    # Increment the count for the given InstanceId
    if ($jsonData.$InstanceId) {
        $jsonData.$InstanceId += 1
    } else {
        $jsonData | Add-Member -MemberType NoteProperty -Name $InstanceId -Value 1
    }

    # Convert back to JSON and save it to the file
    $jsonData | ConvertTo-Json | Set-Content -Path $filePath
}

class CustomClass {
    [string]$Name

    [int]$Age

    [byte[]]$Data

    # There is an issue serializing TimeSpan objects as parameters to Functions in PowerShell. 
    # For now, we will substitute with [string]. See more at https://github.com/Azure/azure-functions-powershell-worker/issues/1110
    [string]$Duration
}

$exportableTypes = @(
    [CustomClass]
)

$TypeAcceleratorsClass = [psobject].Assembly.GetType(
    'System.Management.Automation.TypeAccelerators'
)
# Ensure none of the types would clobber an existing type accelerator.
# If a type accelerator with the same name exists, don't re-register it.
$ExistingTypeAccelerators = $TypeAcceleratorsClass::Get
foreach ($Type in $ExportableTypes) {
    if (!($Type.FullName -in $ExistingTypeAccelerators.Keys)) {
        $TypeAcceleratorsClass::Add($Type.FullName, $Type)
    }
}
# Remove type accelerators when the module is removed.
$MyInvocation.MyCommand.ScriptBlock.Module.OnRemove = {
    foreach($Type in $ExportableTypes) {
        $TypeAcceleratorsClass::Remove($Type.FullName)
    }
}.GetNewClosure()

Export-ModuleMember -Function GetExecutionCount, IncrementExecutionCount
