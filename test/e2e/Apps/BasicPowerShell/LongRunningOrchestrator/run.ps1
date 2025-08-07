#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param($Context)

$output = @()

for ($i = 0; $i -lt 1000; $i++) {
    $output += Invoke-DurableActivity -FunctionName 'SimulatedWorkActivity' -Input 1000
}

$output
