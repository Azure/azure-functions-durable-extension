// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

import { app, HttpHandler, HttpRequest, HttpResponseInit, InvocationContext } from '@azure/functions';
import * as df from 'durable-functions';

// HTTP handler for PurgeOrchestrationHistory
const PurgeOrchestrationHistory: HttpHandler = async (req: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> => {
    context.log('Starting purge all instance history');
    try {
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

        const client = df.getClient(context);

        // Purge orchestration history
        const result = await client.purgeInstanceHistoryBy({
            createdTimeFrom: purgeStartTime,
            createdTimeTo: purgeEndTime,
            runtimeStatus: [
                df.OrchestrationRuntimeStatus.Completed,
                df.OrchestrationRuntimeStatus.Failed,
                df.OrchestrationRuntimeStatus.Terminated
            ]
        });

        context.log('Finished purge all instance history');
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

app.http('PurgeOrchestrationHistory', {
    route: 'PurgeOrchestrationHistory',
    methods: ['GET', 'POST'],
    extraInputs: [df.input.durableClient()],
    handler: PurgeOrchestrationHistory,
});