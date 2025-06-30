#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param($Context)

$output = @()

$output += Invoke-DurableActivity -FunctionName 'RaiseException' -Input ($Context.InstanceId)

$output
