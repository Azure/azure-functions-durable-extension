param($Context)

$nonReplayCount = 0
$replayCount = 0

if ($Context.IsReplaying) { $replayCount++ } else { $nonReplayCount++ }

$r1 = Invoke-DurableActivity -FunctionName 'IsReplayingEcho' -Input 'a'
if ($Context.IsReplaying) { $replayCount++ } else { $nonReplayCount++ }

$r2 = Invoke-DurableActivity -FunctionName 'IsReplayingEcho' -Input 'b'
if ($Context.IsReplaying) { $replayCount++ } else { $nonReplayCount++ }

$r3 = Invoke-DurableActivity -FunctionName 'IsReplayingEcho' -Input 'c'
if ($Context.IsReplaying) { $replayCount++ } else { $nonReplayCount++ }

[ordered]@{
    non_replay_count  = $nonReplayCount
    replay_count      = $replayCount
    total_checkpoints = $nonReplayCount + $replayCount
    activities        = @($r1, $r2, $r3)
}
