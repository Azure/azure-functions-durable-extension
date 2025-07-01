#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param($Context)

$timeoutSeconds = $Context.Input

$task1 = Invoke-DurableActivity -FunctionName 'LongActivity' -Input '' -NoWait
$task2 = Start-DurableTimer -Duration (New-TimeSpan -Seconds $timeoutSeconds) -NoWait
$firstResult = Wait-DurableTask -Task @($task1, $task2) -Any

if ($firstResult -eq $task1) {
    Write-Host "LongActivity completed first, returning result"
    Stop-DurableTimerTask -Task $task2
    Get-DurableTaskResult -Task $task1
} elseif ($firstResult -eq $task2) {
    Write-Host "Timer completed first, returning 'Timeout'"
    'The activity function timed out'
} else {
    Write-Host "Unexpected result type, returning 'Timeout'"
    'The activity function timed out'
}
