// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

import { app, HttpHandler, HttpRequest, HttpResponse, HttpResponseInit, InvocationContext } from '@azure/functions';
import * as df from 'durable-functions';
import { ActivityHandler, OrchestrationContext, OrchestrationHandler } from 'durable-functions';

// Orchestration
const TimeoutOrchestrator: OrchestrationHandler = function* (context: OrchestrationContext) {
    const timeoutSeconds = context.df.getInput();
    if (!timeoutSeconds || typeof timeoutSeconds !== 'number') {
        throw new Error("Timeout value is required for this orchestration.");
    }
    const timeout = timeoutSeconds * 1000;
    const deadline = context.df.currentUtcDateTime.getTime() + timeout;

    const activityTask = context.df.callActivity('long_activity', context.df.instanceId);
    const timeoutTask = context.df.createTimer(new Date(deadline));

    const winner = yield context.df.Task.any([activityTask, timeoutTask]);
    if (winner === activityTask) {
        timeoutTask.cancel();
        return activityTask.result;
    } else {
        return "The activity function timed out";
    }
};
df.app.orchestration('TimeoutOrchestrator', TimeoutOrchestrator);

// Activity
const long_activity: ActivityHandler = async (instanceid: string): Promise<string> => {
    // The duration of 5 seconds for this activity was chosen because
    // it is long enough to demonstrate both the activity timeout and the
    // activity success case in the tests for activity timeout.
    await new Promise(resolve => setTimeout(resolve, 5000));
    return "The activity function completed successfully";
};
df.app.activity('long_activity', { handler: long_activity });

// HTTP starter
const timer_http_start: HttpHandler = async (req: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> => {
    const client = df.getClient(context);
    const timeoutSecondsStr = req.query.get("timeoutSeconds") ?? (await req.text());
    const timeoutSeconds = Number(timeoutSecondsStr);

    if (!timeoutSecondsStr || isNaN(timeoutSeconds)) {
        return {
            status: 400,
            body: "Please pass a valid timeoutSeconds value in the query string or in the request body."
        };
    }

    const instanceId: string = await client.startNew("TimeoutOrchestrator", {input: timeoutSeconds});
    context.log(`Started orchestration with ID = '${instanceId}'.`);
    return client.createCheckStatusResponse(req, instanceId);
};

app.http('TimeoutOrchestrator_HttpStart', {
    route: 'TimeoutOrchestrator_HttpStart',
    methods: ['GET', 'POST'],
    extraInputs: [df.input.durableClient()],
    handler: timer_http_start,
});