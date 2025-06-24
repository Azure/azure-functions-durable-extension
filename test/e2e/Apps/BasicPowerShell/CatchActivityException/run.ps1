#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#
param($Context)

Write-Host $Context.InstanceId

try {
    $output = Invoke-DurableActivity -FunctionName 'RaiseException' -Input $Context.InstanceId
    Write-Host "Activity completed successfully with output: $output"
    $output
}
catch {
    Write-Host "Activity failed with exception: $($_.Exception.Message)"
    $_.Exception.Message
}