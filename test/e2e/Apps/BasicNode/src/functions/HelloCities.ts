import { app, HttpHandler, HttpRequest, HttpResponse, InvocationContext } from '@azure/functions';
import * as df from 'durable-functions';
import { ActivityHandler, OrchestrationContext, OrchestrationHandler } from 'durable-functions';

const activityName = 'HelloCitiesActivity';

const HelloCities: OrchestrationHandler = function* (context: OrchestrationContext) {
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

const StartOrchestration: HttpHandler = async (request: HttpRequest, context: InvocationContext): Promise<HttpResponse> => {
    const client = df.getClient(context);
    const body: unknown = await request.text();
    const orchName = request.params.orchestrationName;
    const instanceId: string = await client.startNew(request.params.orchestrationName, { input: body });

    context.log(`Started orchestration with ID = '${instanceId}'.`);

    return client.createCheckStatusResponse(request, instanceId);
};

app.http('StartOrchestration', {
    route: 'StartOrchestration',
    extraInputs: [df.input.durableClient()],
    handler: StartOrchestration,
});