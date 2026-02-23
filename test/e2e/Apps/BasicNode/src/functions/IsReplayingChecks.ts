// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

import * as df from 'durable-functions';
import { ActivityHandler, OrchestrationContext, OrchestrationHandler } from 'durable-functions';

// ---------------------------------------------------------------------------
// Activity used by the is_replaying orchestrators
// ---------------------------------------------------------------------------

const IsReplayingEcho: ActivityHandler = (input: string): string => {
    return input;
};
df.app.activity('IsReplayingEcho', { handler: IsReplayingEcho });

// ---------------------------------------------------------------------------
// 1. IsReplayingBasic
// ---------------------------------------------------------------------------

const IsReplayingBasic: OrchestrationHandler = function* (context: OrchestrationContext) {
    // Workaround: context.df.isReplaying is undefined before the first yield.
    // See https://github.com/Azure/azure-functions-durable-js/issues/564
    const before: boolean = context.df.isReplaying ?? true;
    const result: string = yield context.df.callActivity('IsReplayingEcho', 'hello');
    const after: boolean = context.df.isReplaying;
    return {
        before_activity: before,
        after_activity: after,
        activity_result: result,
    };
};
df.app.orchestration('IsReplayingBasic', IsReplayingBasic);

// ---------------------------------------------------------------------------
// 2. IsReplayingMultiActivity
// ---------------------------------------------------------------------------

const IsReplayingMultiActivity: OrchestrationHandler = function* (context: OrchestrationContext) {
    // Workaround: context.df.isReplaying is undefined before the first yield.
    // See https://github.com/Azure/azure-functions-durable-js/issues/564
    const snapshots: object[] = [];

    snapshots.push({ step: 0, label: 'start', is_replaying: context.df.isReplaying ?? true });

    const r1: string = yield context.df.callActivity('IsReplayingEcho', 'one');
    snapshots.push({ step: 1, label: 'after_first', is_replaying: context.df.isReplaying });

    const r2: string = yield context.df.callActivity('IsReplayingEcho', 'two');
    snapshots.push({ step: 2, label: 'after_second', is_replaying: context.df.isReplaying });

    const r3: string = yield context.df.callActivity('IsReplayingEcho', 'three');
    snapshots.push({ step: 3, label: 'after_third', is_replaying: context.df.isReplaying });

    return {
        snapshots,
        activities: [r1, r2, r3],
    };
};
df.app.orchestration('IsReplayingMultiActivity', IsReplayingMultiActivity);

// ---------------------------------------------------------------------------
// 3. IsReplayingConditionalLog
// ---------------------------------------------------------------------------

const IsReplayingConditionalLog: OrchestrationHandler = function* (context: OrchestrationContext) {
    // Workaround: context.df.isReplaying is undefined before the first yield.
    // See https://github.com/Azure/azure-functions-durable-js/issues/564
    let liveLogCount = 0;

    if (!(context.df.isReplaying ?? true)) {
        console.log('IsReplayingConditionalLog: LIVE before activity');
        liveLogCount++;
    } else {
        console.log('IsReplayingConditionalLog: REPLAY before activity');
    }

    const result: string = yield context.df.callActivity('IsReplayingEcho', 'logged');

    if (!context.df.isReplaying) {
        console.log('IsReplayingConditionalLog: LIVE after activity');
        liveLogCount++;
    } else {
        console.log('IsReplayingConditionalLog: REPLAY after activity');
    }

    return {
        live_log_count: liveLogCount,
        activity_result: result,
    };
};
df.app.orchestration('IsReplayingConditionalLog', IsReplayingConditionalLog);

// ---------------------------------------------------------------------------
// 4. IsReplayingCounter
// ---------------------------------------------------------------------------

const IsReplayingCounter: OrchestrationHandler = function* (context: OrchestrationContext) {
    // Workaround: context.df.isReplaying is undefined before the first yield.
    // See https://github.com/Azure/azure-functions-durable-js/issues/564
    let nonReplayCount = 0;
    let replayCount = 0;

    if (context.df.isReplaying ?? true) { replayCount++; } else { nonReplayCount++; }

    const r1: string = yield context.df.callActivity('IsReplayingEcho', 'a');
    if (context.df.isReplaying) { replayCount++; } else { nonReplayCount++; }

    const r2: string = yield context.df.callActivity('IsReplayingEcho', 'b');
    if (context.df.isReplaying) { replayCount++; } else { nonReplayCount++; }

    const r3: string = yield context.df.callActivity('IsReplayingEcho', 'c');
    if (context.df.isReplaying) { replayCount++; } else { nonReplayCount++; }

    return {
        non_replay_count: nonReplayCount,
        replay_count: replayCount,
        total_checkpoints: nonReplayCount + replayCount,
        activities: [r1, r2, r3],
    };
};
df.app.orchestration('IsReplayingCounter', IsReplayingCounter);

// ---------------------------------------------------------------------------
// 5. IsReplayingFanOutFanIn
// ---------------------------------------------------------------------------

const IsReplayingFanOutFanIn: OrchestrationHandler = function* (context: OrchestrationContext) {
    // Workaround: context.df.isReplaying is undefined before the first yield.
    // See https://github.com/Azure/azure-functions-durable-js/issues/564
    const before: boolean = context.df.isReplaying ?? true;

    const tasks = [
        context.df.callActivity('IsReplayingEcho', 'alpha'),
        context.df.callActivity('IsReplayingEcho', 'beta'),
        context.df.callActivity('IsReplayingEcho', 'gamma'),
    ];
    const results: string[] = yield context.df.Task.all(tasks);

    const after: boolean = context.df.isReplaying;

    return {
        before_fan_out: before,
        after_fan_in: after,
        activities: results,
    };
};
df.app.orchestration('IsReplayingFanOutFanIn', IsReplayingFanOutFanIn);
