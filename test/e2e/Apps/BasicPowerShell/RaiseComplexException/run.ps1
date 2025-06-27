#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param($InstanceId)

Import-Module -Name "MyHelperModule"

if ((GetExecutionCount $InstanceId) -gt 0) {
    IncrementExecutionCount($InstanceId)
    "Success"
}
else {
    IncrementExecutionCount($InstanceId)
    throw [System.InvalidOperationException]::new("This activity failed\r\nMore information about the failure", [System.OverflowException]::new("Inner exception message"))
}
