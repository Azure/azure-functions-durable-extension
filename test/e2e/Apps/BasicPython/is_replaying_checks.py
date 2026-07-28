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
#    Records is_replaying before and after a single activity call.
#    Result: { "before_activity": true, "after_activity": false,
#              "activity_result": "hello" }
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
#    Snapshots is_replaying at four checkpoints across three sequential
#    activities. First three are replaying; only the last is live.
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
#    Guards log emission with is_replaying. Emits "LIVE" markers only on
#    non-replay paths and "REPLAY" markers otherwise.
#
#    Expected logs (each exactly once):
#      "IsReplayingConditionalLog: LIVE before activity"   – first execution
#      "IsReplayingConditionalLog: LIVE after activity"    – final pass
#      "IsReplayingConditionalLog: REPLAY before activity" – final replay pass
#
#    Result: { "live_log_count": 1, "activity_result": "logged" }
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
#    Tallies replay vs. live checkpoints across three sequential activities.
#    On the final pass, the first three checkpoints are replaying and only
#    the last (after the final yield) is live.
#    Result: { "non_replay_count": 1, "replay_count": 3,
#              "total_checkpoints": 4, "activities": ["a","b","c"] }
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
#    Records is_replaying before fan-out and after fan-in of three parallel
#    activities. Before is replaying; after is live.
#    Result: { "before_fan_out": true, "after_fan_in": false,
#              "activities": ["alpha","beta","gamma"] }
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
