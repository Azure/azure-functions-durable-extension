#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

import logging

import azure.durable_functions as df

bp = df.Blueprint()


# ---------------------------------------------------------------------------
# Activity used by the is_replaying orchestrators
# ---------------------------------------------------------------------------

@bp.activity_trigger(input_name="value")
def is_replaying_echo(value: str) -> str:
    """Simple activity that echoes the input back."""
    return value


# ---------------------------------------------------------------------------
# 1. IsReplayingBasic
#    Records ctx.is_replaying before and after a single activity call.
#    Expected result: { "before_activity": true|false, "after_activity": false,
#                       "activity_result": "<value>" }
#    On first (non-replay) execution:  before_activity = False
#    After replay for the activity:     before_activity = True (replayed),
#                                       after_activity  = False (new code path)
# ---------------------------------------------------------------------------

@bp.orchestration_trigger(context_name="context", orchestration="IsReplayingBasic")
def is_replaying_basic(context: df.DurableOrchestrationContext):
    before = context.is_replaying
    result = yield context.call_activity("is_replaying_echo", "hello")
    after = context.is_replaying
    return {
        "before_activity": before,
        "after_activity": after,
        "activity_result": result,
    }


# ---------------------------------------------------------------------------
# 2. IsReplayingMultiActivity
#    Records is_replaying at each step across three sequential activities.
#    The result contains a list of snapshots taken at each stage.
#    Tests can verify that after the final activity call, is_replaying is
#    False, while earlier checkpoints are True during replay.
# ---------------------------------------------------------------------------

@bp.orchestration_trigger(context_name="context", orchestration="IsReplayingMultiActivity")
def is_replaying_multi_activity(context: df.DurableOrchestrationContext):
    snapshots = []

    # Checkpoint 0 – before any activity
    snapshots.append({"step": 0, "label": "start", "is_replaying": context.is_replaying})

    r1 = yield context.call_activity("is_replaying_echo", "one")

    # Checkpoint 1 – after first activity
    snapshots.append({"step": 1, "label": "after_first", "is_replaying": context.is_replaying})

    r2 = yield context.call_activity("is_replaying_echo", "two")

    # Checkpoint 2 – after second activity
    snapshots.append({"step": 2, "label": "after_second", "is_replaying": context.is_replaying})

    r3 = yield context.call_activity("is_replaying_echo", "three")

    # Checkpoint 3 – after third (final) activity
    snapshots.append({"step": 3, "label": "after_third", "is_replaying": context.is_replaying})

    return {
        "snapshots": snapshots,
        "activities": [r1, r2, r3],
    }


# ---------------------------------------------------------------------------
# 3. IsReplayingConditionalLog
#    Uses is_replaying to emit a log message ONLY on the live (non-replay)
#    execution path. Tests can search the Function Host logs for these
#    specific markers to confirm the guard works correctly.
#
#    Log markers:
#      "IsReplayingConditionalLog: LIVE before activity"
#      "IsReplayingConditionalLog: LIVE after activity"
#    These should each appear exactly ONCE in the logs – the "before" marker
#    is emitted only during the very first execution (before any replay),
#    and the "after" marker is emitted after the activity completes on the
#    live (non-replay) pass.
#    
#    A REPLAY marker is logged when is_replaying is True, which should only
#    appear during replay passes:
#      "IsReplayingConditionalLog: REPLAY before activity"
# ---------------------------------------------------------------------------

@bp.orchestration_trigger(context_name="context", orchestration="IsReplayingConditionalLog")
def is_replaying_conditional_log(context: df.DurableOrchestrationContext):
    live_log_count = 0

    if not context.is_replaying:
        logging.info("IsReplayingConditionalLog: LIVE before activity")
        live_log_count += 1
    else:
        logging.info("IsReplayingConditionalLog: REPLAY before activity")

    result = yield context.call_activity("is_replaying_echo", "logged")

    if not context.is_replaying:
        logging.info("IsReplayingConditionalLog: LIVE after activity")
        live_log_count += 1
    else:
        logging.info("IsReplayingConditionalLog: REPLAY after activity")

    return {
        "live_log_count": live_log_count,
        "activity_result": result,
    }


# ---------------------------------------------------------------------------
# 4. IsReplayingCounter
#    Counts how many times code is reached on a non-replay pass across
#    multiple activities. On a completed orchestration:
#      - non_replay_count should equal the number of activities + 1
#        (start + after each activity on the final replay pass)
#        Actually, the final pass replays through all previous yields
#        (is_replaying=True) and only the code after the last yield is
#        non-replay. So the expected non_replay_count is 1 per complete
#        execution from start-to-finish where the code path after the
#        last yield runs with is_replaying=False.
#
#    Result:
#      { "non_replay_count": <int>, "replay_count": <int>,
#        "total_checkpoints": <int>, "activities": [...] }
# ---------------------------------------------------------------------------

@bp.orchestration_trigger(context_name="context", orchestration="IsReplayingCounter")
def is_replaying_counter(context: df.DurableOrchestrationContext):
    non_replay_count = 0
    replay_count = 0

    # Checkpoint: start
    if context.is_replaying:
        replay_count += 1
    else:
        non_replay_count += 1

    r1 = yield context.call_activity("is_replaying_echo", "a")

    # Checkpoint: after activity 1
    if context.is_replaying:
        replay_count += 1
    else:
        non_replay_count += 1

    r2 = yield context.call_activity("is_replaying_echo", "b")

    # Checkpoint: after activity 2
    if context.is_replaying:
        replay_count += 1
    else:
        non_replay_count += 1

    r3 = yield context.call_activity("is_replaying_echo", "c")

    # Checkpoint: after activity 3 (final – never replayed)
    if context.is_replaying:
        replay_count += 1
    else:
        non_replay_count += 1

    return {
        "non_replay_count": non_replay_count,
        "replay_count": replay_count,
        "total_checkpoints": non_replay_count + replay_count,
        "activities": [r1, r2, r3],
    }


# ---------------------------------------------------------------------------
# 5. IsReplayingFanOutFanIn
#    Demonstrates is_replaying behaviour with fan-out/fan-in (parallel).
#    All tasks are scheduled before any of them complete, so the replay
#    characteristics differ from sequential calls.
#
#    Result:
#      { "before_fan_out": <bool>, "after_fan_in": <bool>,
#        "activities": [...] }
# ---------------------------------------------------------------------------

@bp.orchestration_trigger(context_name="context", orchestration="IsReplayingFanOutFanIn")
def is_replaying_fan_out_fan_in(context: df.DurableOrchestrationContext):
    before = context.is_replaying

    tasks = [
        context.call_activity("is_replaying_echo", "alpha"),
        context.call_activity("is_replaying_echo", "beta"),
        context.call_activity("is_replaying_echo", "gamma"),
    ]
    results = yield context.task_all(tasks)

    after = context.is_replaying

    return {
        "before_fan_out": before,
        "after_fan_in": after,
        "activities": results,
    }
