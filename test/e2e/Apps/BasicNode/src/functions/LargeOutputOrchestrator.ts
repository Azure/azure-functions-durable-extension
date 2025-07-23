// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

import { app, HttpHandler, HttpRequest, HttpResponse, HttpResponseInit, InvocationContext } from '@azure/functions';
import * as df from 'durable-functions';
import { ActivityHandler, OrchestrationContext, OrchestrationHandler } from 'durable-functions';

// Helper function
function generateLargeString(sizeInKB: number): string {
    return 'A'.repeat(sizeInKB * 1024);
}

// Orchestration
const LargeOutputOrchestrator: OrchestrationHandler = function* (context: OrchestrationContext) {
    const sizeInKB = context.df.getInput<number>();
    context.log('Saying hello.');
    const outputs: any[] = [];
    const r_1 = yield context.df.callActivity('large_output_say_hello', 'Tokyo');
    outputs.push(r_1);
    outputs.push(generateLargeString(sizeInKB));
    return outputs;
};
df.app.orchestration('LargeOutputOrchestrator', LargeOutputOrchestrator);

// Activity
const large_output_say_hello: ActivityHandler = (name: string): string => {
    return `Hello ${name}!`;
};
df.app.activity('large_output_say_hello', { handler: large_output_say_hello });

// HTTP starter
const LargeOutputOrchestrator_HttpStart: HttpHandler = async (req: HttpRequest, context: InvocationContext): Promise<HttpResponse> => {
    const client = df.getClient(context);
    let sizeInKB: number = 0;
    try {
        const body = await req.json();
        sizeInKB = parseInt(body as any, 10);
    } catch {
        sizeInKB = parseInt(req.query.get('sizeInKB') ?? '0', 10);
    }
    const instanceId = await client.startNew('LargeOutputOrchestrator', {input: sizeInKB});
    context.log(`Started orchestration with ID = '${instanceId}'.`);
    return client.createCheckStatusResponse(req, instanceId);
};

app.http('LargeOutputOrchestrator_HttpStart', {
    route: 'LargeOutputOrchestrator_HttpStart',
    methods: ['GET', 'POST'],
    extraInputs: [df.input.durableClient()],
    handler: LargeOutputOrchestrator_HttpStart,
});

// HTTP query output
const LargeOutputOrchestrator_Query_Output: HttpHandler = async (req: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> => {
    const client = df.getClient(context);
    const id = req.params["id"] ?? req.query.get('id');
    const metadata = await client.getStatus(id, { showInput: true });
    if (!metadata) {
        return { status: 404, 
            body: 'Orchestration metadata not found.' };
    }
    const output = metadata.output;
    return {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(output)
    };
};

app.http('LargeOutputOrchestrator_Query_Output', {
    route: 'LargeOutputOrchestrator_Query_Output',
    methods: ['GET', 'POST'],
    extraInputs: [df.input.durableClient()],
    handler: LargeOutputOrchestrator_Query_Output,
});