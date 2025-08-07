// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

import * as df from "durable-functions";
import { OrchestrationContext, OrchestrationHandler, EntityContext, EntityHandler } from "durable-functions";
import { InvalidOperationException, OverflowException } from "../Shared/ExceptionTypes";

// In-memory attempt count (not durable, but matches Python's global dict for test purposes)
const attemptCount: Record<string, number> = {};

// Orchestration: ThrowEntityOrchestration
const ThrowEntityOrchestration: OrchestrationHandler = function* (context: OrchestrationContext) {
    const entityId = new df.EntityId("Counter", "myCounter");
    yield context.df.callEntity(entityId, "get", context.df.instanceId);
    return "Success";
};
df.app.orchestration("ThrowEntityOrchestration", ThrowEntityOrchestration);

// Orchestration: CatchEntityOrchestration
const CatchEntityOrchestration: OrchestrationHandler = function* (context: OrchestrationContext) {
    const entityId = new df.EntityId("Counter", "myCounter");
    try {
        yield context.df.callEntity(entityId, "get", context.df.instanceId);
        return "Success";
    } catch (e: any) {
        return String(e);
    }
};
df.app.orchestration("CatchEntityOrchestration", CatchEntityOrchestration);

// Orchestration: RetryEntityOrchestration
const RetryEntityOrchestration: OrchestrationHandler = function* (context: OrchestrationContext) {
    const entityId = new df.EntityId("Counter", "myCounter");
    try {
        yield context.df.callEntity(entityId, "get", context.df.instanceId);
        return "Success";
    } catch (e) {
        yield context.df.callEntity(entityId, "get", context.df.instanceId);
        return "Success";
    }
};
df.app.orchestration("RetryEntityOrchestration", RetryEntityOrchestration);

// Entity: Counter
const Counter: EntityHandler<string> = (context: EntityContext<string>) => {
    const instanceId = context.df.getInput<string>();
    if (!instanceId) {
        throw new Error("Did not get a valid instanceId as input to the entity");
    }
    if (!(instanceId in attemptCount)) {
        attemptCount[instanceId] = 1;
        const inner = new OverflowException("Inner exception message");
        const err = new InvalidOperationException("This entity failed\r\nMore information about the failure");
        err.cause = inner;
        throw err;
    }
    attemptCount[instanceId] += 1;
    context.df.return(0);
};
df.app.entity("Counter", Counter);