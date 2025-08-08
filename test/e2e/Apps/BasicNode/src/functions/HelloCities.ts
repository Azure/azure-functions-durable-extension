// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

import { app, HttpHandler, HttpRequest, HttpResponse, InvocationContext } from '@azure/functions';
import * as df from 'durable-functions';
import { ActivityHandler, OrchestrationContext, OrchestrationHandler } from 'durable-functions';

const activityName = 'HelloCitiesActivity';

const HelloCities: OrchestrationHandler = function* (context: OrchestrationContext) {
    const scheduled_start_time = context.df.getInput<string>();
    if (scheduled_start_time) {
        let scheduled_start_time_date = new Date(scheduled_start_time);
        yield context.df.createTimer(scheduled_start_time_date);
    }

    const outputs = [];
    outputs.push(yield context.df.callActivity(activityName, 'Tokyo'));
    outputs.push(yield context.df.callActivity(activityName, 'Seattle'));
    outputs.push(yield context.df.callActivity(activityName, 'London'));

    return outputs;
};
df.app.orchestration('HelloCities', HelloCities);

const HelloCitiesActivity: ActivityHandler = (input: string): string => {
    return `Hello ${input}!`;
};
df.app.activity(activityName, { handler: HelloCitiesActivity });


const HelloCitiesHttpStartScheduled: HttpHandler = async (request: HttpRequest, context: InvocationContext): Promise<HttpResponse> => {
    const client = df.getClient(context);
    const body: unknown = await request.text();
    const instanceId: string = await client.startNew("HelloCities", { input: request.params.ScheduledStartTime });

    context.log(`Started orchestration with ID = '${instanceId}'.`);

    return client.createCheckStatusResponse(request, instanceId);
};

app.http('HelloCities_HttpStart_Scheduled', {
    route: 'HelloCities_HttpStart_Scheduled',
    extraInputs: [df.input.durableClient()],
    handler: HelloCitiesHttpStartScheduled,
});


const StartOrchestration: HttpHandler = async (request: HttpRequest, context: InvocationContext): Promise<HttpResponse> => {
    const client = df.getClient(context);
    const instanceId: string = await client.startNew(request.params.orchestrationName);

    context.log(`Started orchestration with ID = '${instanceId}'.`);

    return client.createCheckStatusResponse(request, instanceId);
};

app.http('StartOrchestration', {
    route: 'StartOrchestration',
    extraInputs: [df.input.durableClient()],
    handler: StartOrchestration,
});
