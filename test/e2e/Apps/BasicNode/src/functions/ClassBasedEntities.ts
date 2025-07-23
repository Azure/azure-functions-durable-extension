// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

import * as df from "durable-functions";
import { OrchestrationContext, OrchestrationHandler, EntityContext, EntityHandler } from "durable-functions";

// Orchestration
const ClassBasedEntityOrchestration: OrchestrationHandler = function* (context: OrchestrationContext) {
    const entityId = new df.EntityId("TestEntity", "singleton");
    yield context.df.callEntity(entityId, "SetState", 42);
    const result = yield context.df.callEntity(entityId, "GetState");
    return result;
};
df.app.orchestration("ClassBasedEntityOrchestration", ClassBasedEntityOrchestration);

// Entity
const TestEntity: EntityHandler<{state: string}> = (context: EntityContext<{state: string}>) => {
    if (context.df.operationName === "SetState") {
        const currentState = context.df.getState(() => ({ state: "" }));
        currentState.state = `IConfiguration: yes, MyInjectedService: yes, BlobContainerClient: yes, Number: ${context.df.getInput()}`;
        context.df.setState(currentState);
    } else if (context.df.operationName === "GetState") {
        const currentState = context.df.getState();
        if (!currentState) {
            throw new Error("State not set");
        }
        context.df.return(currentState.state);
    }
};
df.app.entity("TestEntity", TestEntity);