#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param($Context)

$output = @()

$output += Invoke-DurableActivity -FunctionName 'SayHello' -Input 'Tokyo'
$output += Invoke-DurableActivity -FunctionName 'SayHello' -Input 'Seattle'
$output += Invoke-DurableActivity -FunctionName 'SayHello' -Input 'London'

$output
