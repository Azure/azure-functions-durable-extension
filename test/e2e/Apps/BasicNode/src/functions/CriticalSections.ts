// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

// Critical-section E2E coverage for the Node worker.
//
// These orchestrations exercise `context.df.lock(...)` and the locking-rule
// enforcement that the durable-functions JS worker layers on top of OOProc
// schema V4 (added in the JS worker PR that pairs with this extension change).
//
// All paired tests in CriticalSectionsTests.cs are currently skipped because
// the JS worker change has not been released. The `@ts-expect-error` markers
// below silence compile errors against the published `durable-functions`
// package — they will start erroring once the package exposes `lock` /
// `DurableLock`, forcing a cleanup pass alongside the dependency bump.

import * as df from "durable-functions";
import {
    EntityContext,
    EntityHandler,
    OrchestrationContext,
    OrchestrationHandler,
} from "durable-functions";

const accountEntityName = "CriticalSectionsAccount";

// ----------------------------------------------------------------------------
// Account entity: int balance, supports get / set / add.
// ----------------------------------------------------------------------------
const accountEntity: EntityHandler<number> = function (context: EntityContext<number>): void {
    let balance: number = context.df.getState(() => 0);

    switch (context.df.operationName) {
        case "set":
            balance = context.df.getInput<number>();
            break;
        case "add":
            balance += context.df.getInput<number>();
            break;
        case "get":
            context.df.return(balance);
            break;
    }

    context.df.setState(balance);
};
df.app.entity(accountEntityName, accountEntity);

// ----------------------------------------------------------------------------
// CriticalSectionLockedTransfer
//
// Seeds two accounts via signals, acquires both locks, debits one, credits
// the other, releases the locks, and returns the final balances as
// "from=<n>;to=<n>". Tests assert the exact output string.
//
// Hardcoded keys (no input) so the existing StartOrchestration handler
// (which doesn't forward query input to the orchestration) can drive it.
// ----------------------------------------------------------------------------
const lockedTransfer: OrchestrationHandler = function* (context: OrchestrationContext) {
    const fromKey = "A";
    const toKey = "B";
    const amount = 30;

    const src = new df.EntityId(accountEntityName, fromKey);
    const dst = new df.EntityId(accountEntityName, toKey);

    // Seed balances. Signals do not need to be inside the critical section.
    context.df.signalEntity(src, "set", 100);
    context.df.signalEntity(dst, "set", 0);

    const lock = yield context.df.lock(src, dst);
    try {
        const fromBalance: number = yield context.df.callEntity(src, "get");
        if (fromBalance < amount) {
            return `insufficient:${fromBalance}`;
        }
        yield context.df.callEntity(src, "add", -amount);
        yield context.df.callEntity(dst, "add", amount);
    } finally {
        lock.release();
    }

    // Re-read the balances *outside* the section so the assertion reflects
    // the committed state visible after release.
    const finalFrom: number = yield context.df.callEntity(src, "get");
    const finalTo: number = yield context.df.callEntity(dst, "get");
    return `from=${finalFrom};to=${finalTo}`;
};
df.app.orchestration("CriticalSectionLockedTransfer", lockedTransfer);

// ----------------------------------------------------------------------------
// CriticalSectionNestedLockViolation
//
// Acquires a lock and then attempts to acquire a second one. The JS worker
// must throw LockingRulesViolationError on the inner `lock` call. The
// orchestration is expected to FAIL (we let the exception propagate so the
// test can assert RuntimeStatus == "Failed").
// ----------------------------------------------------------------------------
const nestedLockViolation: OrchestrationHandler = function* (context: OrchestrationContext) {
    const eA = new df.EntityId(accountEntityName, "A");
    const eB = new df.EntityId(accountEntityName, "B");

    const lock = yield context.df.lock(eA);
    try {
        // This call must throw LockingRulesViolationError synchronously.
        yield context.df.lock(eB);
        return "reached-after-inner-lock"; // should not be returned
    } finally {
        lock.release();
    }
};
df.app.orchestration("CriticalSectionNestedLockViolation", nestedLockViolation);

// ----------------------------------------------------------------------------
// CriticalSectionTimedLockHold
//
// Acquires a lock on a single hardcoded entity, holds it for ~5 seconds via
// createTimer, then releases. Used by the mutual-exclusion test that starts
// two instances simultaneously: the winner takes ~5 s end-to-end, while the
// loser is blocked at `lock(...)` for ~5 s and then runs its own 5 s hold —
// roughly ~10 s end-to-end. The C# test asserts the loser's elapsed time is
// >= 2 × holdSec - slack.
// ----------------------------------------------------------------------------
const HOLD_LOCK_SECONDS = 5;
const timedLockHold: OrchestrationHandler = function* (context: OrchestrationContext) {
    const e = new df.EntityId(accountEntityName, "TimedHoldShared");

    const lock = yield context.df.lock(e);
    try {
        const wakeAt = new Date(context.df.currentUtcDateTime.getTime() + HOLD_LOCK_SECONDS * 1000);
        yield context.df.createTimer(wakeAt);
        return "held";
    } finally {
        lock.release();
    }
};
df.app.orchestration("CriticalSectionTimedLockHold", timedLockHold);
