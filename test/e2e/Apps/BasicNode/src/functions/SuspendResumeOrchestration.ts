// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

import { app, HttpHandler, HttpRequest, HttpResponseInit, InvocationContext } from '@azure/functions';
import * as df from 'durable-functions';
import { DurableClient } from 'durable-functions';

// SuspendInstance HTTP trigger
const SuspendInstance: HttpHandler = async (request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> => {
    const client: DurableClient = df.getClient(context);
    const instanceId = request.params.instanceId;
    const suspendReason = "Suspending the instance for test.";
    try {
        // Reason for the cast - Bug: https://github.com/Azure/azure-functions-durable-js/issues/608
        await (client as any).suspend(instanceId, suspendReason);
        return { status: 200 };
    } catch (ex: any) {
        context.log(`Error suspending instance: ${ex}`);
        return {
            status: 400,
            body: String(ex),
            headers: { "Content-Type": "text/plain" }
        };
    }
};

app.http('SuspendInstance', {
    route: 'SuspendInstance',
    methods: ['GET', 'POST'],
    extraInputs: [df.input.durableClient()],
    handler: SuspendInstance,
});

// ResumeInstance HTTP trigger
const ResumeInstance: HttpHandler = async (request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> => {
    const client = df.getClient(context);
    const instanceId = request.params.instanceId;
    const resumeReason = "Resuming the instance for test.";
    try {
        // Reason for the cast - Bug: https://github.com/Azure/azure-functions-durable-js/issues/608
        await (client as any).resume(instanceId, resumeReason);
        return { status: 200 };
    } catch (ex: any) {
        context.log(`Error resuming instance: ${ex}`);
        return {
            status: 400,
            body: String(ex),
            headers: { "Content-Type": "text/plain" }
        };
    }
};

app.http('ResumeInstance', {
    route: 'ResumeInstance',
    methods: ['GET', 'POST'],
    extraInputs: [df.input.durableClient()],
    handler: ResumeInstance,
});