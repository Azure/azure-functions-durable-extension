// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

import { app, HttpHandler, HttpRequest, HttpResponse, InvocationContext } from '@azure/functions';
import * as df from 'durable-functions';
import { ActivityHandler, OrchestrationContext, OrchestrationHandler } from 'durable-functions';

// Orchestration
const LongRunningOrchestrator: OrchestrationHandler = function* (context: OrchestrationContext) {
    context.log('Starting long-running orchestration.');
    const outputs: string[] = [];
    for (let i = 0; i < 100000; i++) {
        const res: string = yield context.df.callActivity('simulated_work_activity', 100);
        outputs.push(res);
    }
    return outputs;
};
df.app.orchestration('LongRunningOrchestrator', LongRunningOrchestrator);

// Activity
const simulated_work_activity: ActivityHandler = (sleepms: number): string => {
    console.log(`Sleeping for ${sleepms}ms.`);
    // Simulate sleep (busy wait, not recommended for production)
    const start = Date.now();
    while (Date.now() - start < sleepms) { /* busy wait */ }
    return `Slept for ${sleepms}ms.`;
};
df.app.activity('simulated_work_activity', { handler: simulated_work_activity });

// HTTP Terminate Instance
const TerminateInstance: HttpHandler = async (req: HttpRequest, context: InvocationContext): Promise<HttpResponse> => {
    const client = df.getClient(context);
    let body = await req.json().catch(() => ({}));
    let instanceId = await body["instanceId"] || req.query.get("instanceId") || req.params["instanceId"];
    const reason = 'Long-running orchestration was terminated early.';
    try {
        await client.terminate(instanceId, reason);
        return new HttpResponse({ status: 200 });
    } catch (ex: any) {
        return new HttpResponse({ body: String(ex), status: 400, headers: { 'Content-Type': 'text/plain' } });
    }
};

app.http('TerminateInstance', {
    route: 'TerminateInstance',
    methods: ['GET', 'POST'],
    extraInputs: [df.input.durableClient()],
    handler: TerminateInstance,
});