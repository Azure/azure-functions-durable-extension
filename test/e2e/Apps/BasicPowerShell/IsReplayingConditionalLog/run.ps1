param($Context)

$liveLogCount = 0

if (-not $Context.IsReplaying) {
    Write-Host "IsReplayingConditionalLog: LIVE before activity"
    $liveLogCount++
} else {
    Write-Host "IsReplayingConditionalLog: REPLAY before activity"
}

$result = Invoke-DurableActivity -FunctionName 'IsReplayingEcho' -Input 'logged'

if (-not $Context.IsReplaying) {
    Write-Host "IsReplayingConditionalLog: LIVE after activity"
    $liveLogCount++
} else {
    Write-Host "IsReplayingConditionalLog: REPLAY after activity"
}

[ordered]@{
    live_log_count  = $liveLogCount
    activity_result = $result
}
