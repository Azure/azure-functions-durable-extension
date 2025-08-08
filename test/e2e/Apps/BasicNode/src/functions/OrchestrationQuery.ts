// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

import { app, HttpHandler, HttpRequest, HttpResponseInit, InvocationContext } from '@azure/functions';
import * as df from 'durable-functions';

// GetAllInstances
const GetAllInstances: HttpHandler = async (request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> => {
    const client = df.getClient(context);
    try {
        const instances = await client.getStatusAll();
        // This would not be necessary if we implemented toJSON for DurableOrchestrationStatus
        const result = JSON.stringify(instances);
        context.log(result);
        return {
            status: 200,
            headers: { 'Content-Type': 'application/json' },
            body: result
        };
    } catch (ex: any) {
        context.log(`Error: ${ex}`);
        return{
            status: 400,
            headers: { 'Content-Type': 'text/plain' },
            body: String(ex)
        };
    }
};

app.http('GetAllInstances', {
    route: 'GetAllInstances',
    extraInputs: [df.input.durableClient()],
    handler: GetAllInstances,
});

// GetRunningInstances
const GetRunningInstances: HttpHandler = async (request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> => {
    const client = df.getClient(context);
    try {
        const filterStatuses = [
            df.OrchestrationRuntimeStatus.Running,
            df.OrchestrationRuntimeStatus.Pending,
            df.OrchestrationRuntimeStatus.ContinuedAsNew
        ];
        const instances = await client.getStatusBy({ runtimeStatus: filterStatuses });
        const result = JSON.stringify(instances);
        context.log(result);
        return {
            status: 200,
            headers: { 'Content-Type': 'application/json' },
            body: result
        };
    } catch (ex: any) {
        context.log(`Error: ${ex}`);
        return {
            status: 400,
            headers: { 'Content-Type': 'text/plain' },
            body: String(ex)
        };
    }
};

app.http('GetRunningInstances', {
    route: 'GetRunningInstances',
    extraInputs: [df.input.durableClient()],
    handler: GetRunningInstances,
});