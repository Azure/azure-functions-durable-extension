// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

import * as df from 'durable-functions';
import { OrchestrationContext, OrchestrationHandler } from 'durable-functions';

// Orchestrator that schedules the disabled-but-still-deployed DisabledActivity. Because the activity
// has no active listener, the dispatch must fail the orchestration deterministically instead of
// poison-looping forever. See https://github.com/Azure/azure-functions-durable-extension/issues/3471.
const CallDisabledActivity: OrchestrationHandler = function* (context: OrchestrationContext) {
    return yield context.df.callActivity('DisabledActivity', 'hello');
};

df.app.orchestration('CallDisabledActivity', CallDisabledActivity);

// Companion orchestrator for the entity dispatch path: calling an operation on the disabled-but-still-
// deployed DisabledEntity must fail the orchestration deterministically rather than poison-looping.
const CallDisabledEntity: OrchestrationHandler = function* (context: OrchestrationContext) {
    const entityId = new df.EntityId('DisabledEntity', 'disabled-key');
    yield context.df.callEntity(entityId, 'someOperation');
    return 'should not reach here';
};

df.app.orchestration('CallDisabledEntity', CallDisabledEntity);
