param($Context)

$before = $Context.IsReplaying

$task1 = Invoke-DurableActivity -FunctionName 'IsReplayingEcho' -Input 'alpha' -NoWait
$task2 = Invoke-DurableActivity -FunctionName 'IsReplayingEcho' -Input 'beta' -NoWait
$task3 = Invoke-DurableActivity -FunctionName 'IsReplayingEcho' -Input 'gamma' -NoWait

$results = Wait-DurableTask -Task @($task1, $task2, $task3)

$after = $Context.IsReplaying

[ordered]@{
    before_fan_out = $before
    after_fan_in   = $after
    activities     = @(
        (Get-DurableTaskResult -Task $task1),
        (Get-DurableTaskResult -Task $task2),
        (Get-DurableTaskResult -Task $task3)
    )
}
