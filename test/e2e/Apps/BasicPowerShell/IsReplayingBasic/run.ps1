param($Context)

$before = $Context.IsReplaying
$result = Invoke-DurableActivity -FunctionName 'IsReplayingEcho' -Input 'hello'
$after = $Context.IsReplaying

[ordered]@{
    before_activity  = $before
    after_activity   = $after
    activity_result  = $result
}
