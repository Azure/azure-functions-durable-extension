#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

# Orchestrator that schedules the disabled-but-still-deployed DisabledActivity. Because the activity
# has no active listener, the dispatch must fail the orchestration deterministically instead of
# poison-looping forever. See https://github.com/Azure/azure-functions-durable-extension/issues/3471.
param($Context)

$output = @()

$output += Invoke-DurableActivity -FunctionName 'DisabledActivity' -Input 'hello'

$output
