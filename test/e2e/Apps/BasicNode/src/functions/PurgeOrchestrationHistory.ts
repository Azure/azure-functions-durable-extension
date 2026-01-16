// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

import { app, HttpHandler, HttpRequest, HttpResponseInit, InvocationContext } from '@azure/functions';
import { OrchestrationContext, OrchestrationHandler, EntityContext, EntityHandler } from "durable-functions";
import * as df from 'durable-functions';

// HTTP handler for PurgeOrchestrationHistory
const PurgeOrchestrationHistory: HttpHandler = async (req: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> => {
    context.log('Starting to purge instance histories');
    try {
        const client = df.getClient(context);
        let result: df.PurgeHistoryResult;

        const instanceId = req.query.get('instanceId');
        if (instanceId)
        {
            result = await client.purgeInstanceHistory(instanceId);
            context.log(`Finished purging history for instance ${instanceId}`);
        }
        else
        {
            // Parse optional query parameters for purgeStartTime and purgeEndTime
            let purgeStartTime: Date | undefined = undefined;
            let purgeEndTime: Date | undefined = undefined;

            const purgeStartTimeParam = req.query.get('purgeStartTime');
            const purgeEndTimeParam = req.query.get('purgeEndTime');

            if (purgeStartTimeParam) {
                purgeStartTime = new Date(purgeStartTimeParam);
            }
            if (purgeEndTimeParam) {
                purgeEndTime = new Date(purgeEndTimeParam);
            }

            // Purge orchestration history
            result = await client.purgeInstanceHistoryBy({
                createdTimeFrom: purgeStartTime,
                createdTimeTo: purgeEndTime,
                runtimeStatus: [
                    df.OrchestrationRuntimeStatus.Completed,
                    df.OrchestrationRuntimeStatus.Failed,
                    df.OrchestrationRuntimeStatus.Terminated
                ]
            });

            context.log('Finished purge all instance history');
        }
        return {
            status: 200,
            body: `Purged ${result.instancesDeleted} records`,
            headers: { 'Content-Type': 'text/plain' }
        };
    } catch (ex: any) {
        context.error('Failed to purge all instance history', ex);
        return {
            status: 500,
            body: `Failed to purge all instance history: ${ex?.message ?? ex}`,
            headers: { 'Content-Type': 'text/plain' }
        };
    }
};

const InvokeDummyEntityOrchestration: OrchestrationHandler = function* (context: OrchestrationContext) {
    const entityId = new df.EntityId("DummyEntity", "myEntity");
    yield context.df.callEntity(entityId, "get");
    return "Success";
};
df.app.orchestration("InvokeDummyEntityOrchestration", InvokeDummyEntityOrchestration);

const DummyEntity: EntityHandler<string> = (context: EntityContext<string>) => {
    context.df.setState("state");
    context.df.return(0);
};
df.app.entity("DummyEntity", DummyEntity);

app.http('PurgeOrchestrationHistory', {
    route: 'PurgeOrchestrationHistory',
    methods: ['GET', 'POST'],
    extraInputs: [df.input.durableClient()],
    handler: PurgeOrchestrationHistory,
});