#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param ($Context)

$retryOptions = New-DurableRetryOptions -FirstRetryInterval (New-TimeSpan -Seconds 1) -MaxNumberOfAttempts 3 

$output += Invoke-DurableActivity -FunctionName 'RaiseComplexException' -Input $Context.InstanceId -RetryOptions $retryOptions

$output
