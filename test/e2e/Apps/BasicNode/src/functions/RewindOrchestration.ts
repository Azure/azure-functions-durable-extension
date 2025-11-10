// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

import { app, HttpHandler, HttpRequest, HttpResponse, InvocationContext } from '@azure/functions';
import * as df from 'durable-functions';
import { ActivityHandler, OrchestrationContext, OrchestrationHandler, EntityContext, EntityHandler } from 'durable-functions';
 
const invocationCounts: Map<string, number> = new Map();
const entityId = new df.EntityId('InvocationCounterEntity', 'entity');

// Orchestration
const RewindParentOrchestration: OrchestrationHandler = function* (context: OrchestrationContext) {
    const input = context.df.getInput<OrchestrationInput>();

    if (input.name == 'run')
    {
        // Create a timer for 10 minutes
        yield context.df.createTimer(new Date(Date.now() + 60 * 10 * 1000));
    }
    else if (input.name == 'complete')
    {
        return {};
    }
    else if (input.name == 'fail')
    {
        const subOrchestrationTasks = [
            context.df.callSubOrchestrator('SucceedSubOrchestration', 'succeed_sub_1'),
            context.df.callSubOrchestrator(
                'FailParentSubOrchestration',
                new OrchestrationInput('fail_parent_sub_1', input.numFailures, input.callEntities)),
            context.df.callSubOrchestrator(
                'FailParentSubOrchestration',
                new OrchestrationInput('fail_parent_sub_2', input.numFailures, input.callEntities)),
            context.df.callSubOrchestrator('SucceedSubOrchestration', 'succeed_sub_2')
        ];

        yield context.df.Task.all(subOrchestrationTasks);
        return invocationCounts;
    }
    else
    {
        throw new Error('Invalid input');
    }
};
df.app.orchestration('RewindParentOrchestration', RewindParentOrchestration);

// Suborchestrations
const FailParentSubOrchestration: OrchestrationHandler = function* (context: OrchestrationContext) {
    const input = context.df.getInput<OrchestrationInput>();
    yield context.df.callActivity('SucceedActivity', input.name + '_succeed_activity');
    if (input.callEntities)
    {
        yield context.df.callEntity(entityId, input.name + '_call_entity');
    }
    yield context.df.callSubOrchestrator(
        'FailChildSubOrchestration',
        new OrchestrationInput(input.name + '_child', input.numFailures, input.callEntities));
}
df.app.orchestration('FailParentSubOrchestration', FailParentSubOrchestration);

const FailChildSubOrchestration: OrchestrationHandler = function* (context: OrchestrationContext) {
    const input = context.df.getInput<OrchestrationInput>();

    if (input.callEntities)
    {
        context.df.signalEntity(entityId, input.name + '_signal_entity');
    }

    const activityAndEntityTasks  = [
        context.df.callActivity('SucceedActivity', input.name + '_succeed_activity'),
        context.df.callActivity('FailActivity', new OrchestrationInput(input.name + '_fail_activity_1', input.numFailures, input.callEntities)),
        context.df.callActivity('FailActivity', new OrchestrationInput(input.name + '_fail_activity_2', input.numFailures, input.callEntities)),
    ];

    if (input.callEntities)
    {
        activityAndEntityTasks.push(context.df.callEntity(entityId, input.name + '_call_entity'));
    }

    yield context.df.Task.all(activityAndEntityTasks);
}
df.app.orchestration('FailChildSubOrchestration', FailChildSubOrchestration);

const SucceedSubOrchestration: OrchestrationHandler = function* (context: OrchestrationContext) {
    yield context.df.callActivity('SucceedActivity', context.df.getInput<string>() + '_succeed_activity');
}
df.app.orchestration('SucceedSubOrchestration', SucceedSubOrchestration);

// Activities
const SucceedActivity: ActivityHandler = (input: string): string => {
    UpdateInvocationCount(input);
    return 'Hello ' + input;
};
df.app.activity('SucceedActivity', { handler: SucceedActivity });

const FailActivity: ActivityHandler = (input: OrchestrationInput): string => {
    if (UpdateInvocationCount(input.name) <= input.numFailures)
    {
        throw new Error('Failure!');
    }
    return 'Success!';
};
df.app.activity('FailActivity', { handler: FailActivity });

// HTTP Rewind Instance
const RewindInstance: HttpHandler = async (req: HttpRequest, context: InvocationContext): Promise<HttpResponse> => {
    const client = df.getClient(context);
    let body = await req.json().catch(() => ({}));
    let instanceId = await body['instanceId'] || req.query.get('instanceId') || req.params['instanceId'];
    const reason = 'Rewinding the instance for testing.';
    try {
        await client.rewind(instanceId, reason);
        return new HttpResponse({ status: 200 });
    } catch (ex: any) {
        return new HttpResponse({ body: String(ex), status: 400, headers: { 'Content-Type': 'text/plain' } });
    }
};

app.http('RewindInstance', {
    route: 'RewindInstance',
    methods: ['GET', 'POST'],
    extraInputs: [df.input.durableClient()],
    handler: RewindInstance,
});

const HttpStart_RewindOrchestration: HttpHandler = async (request: HttpRequest, context: InvocationContext): Promise<HttpResponse> => {
    const client = df.getClient(context);
    invocationCounts.clear();

    const instanceId: string = await client.startNew(
        'RewindParentOrchestration',
        { input: new OrchestrationInput(
            request.params.input,
            Number.parseInt(request.params.numFailures),
            request.params.callEntities == 'true'
        )});

    context.log(`Started orchestration with ID = '${instanceId}'.`);

    return client.createCheckStatusResponse(request, instanceId);
};

app.http('HttpStart_RewindOrchestration', {
    route: 'HttpStart_RewindOrchestration',
    extraInputs: [df.input.durableClient()],
    handler: HttpStart_RewindOrchestration,
});

function UpdateInvocationCount(key) {
  let invocationCount = invocationCounts.get(key) || 0; // get existing count or 0
  invocationCounts.set(key, ++invocationCount);
  return invocationCount;
}

// Entity
const InvocationCounterEntity: EntityHandler<{state: string}> = (context: EntityContext<{state: string}>) => {
    UpdateInvocationCount(context.df.operationName);  
};
df.app.entity('InvocationCounterEntity', InvocationCounterEntity);

class OrchestrationInput
{
    name: string;
    numFailures: number;
    callEntities: boolean;

    constructor(name: string, numFailures: number, callEntities: boolean)
    {
        this.name = name;
        this.numFailures = numFailures;
        this.callEntities = callEntities;
    }
}