param($Context)

$snapshots = @()

$snapshots += [ordered]@{ step = 0; label = "start"; is_replaying = $Context.IsReplaying }

$r1 = Invoke-DurableActivity -FunctionName 'IsReplayingEcho' -Input 'one'
$snapshots += [ordered]@{ step = 1; label = "after_first"; is_replaying = $Context.IsReplaying }

$r2 = Invoke-DurableActivity -FunctionName 'IsReplayingEcho' -Input 'two'
$snapshots += [ordered]@{ step = 2; label = "after_second"; is_replaying = $Context.IsReplaying }

$r3 = Invoke-DurableActivity -FunctionName 'IsReplayingEcho' -Input 'three'
$snapshots += [ordered]@{ step = 3; label = "after_third"; is_replaying = $Context.IsReplaying }

[ordered]@{
    snapshots  = $snapshots
    activities = @($r1, $r2, $r3)
}
