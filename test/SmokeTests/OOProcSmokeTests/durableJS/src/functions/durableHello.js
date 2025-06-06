const { app } = require('@azure/functions');
const df = require('durable-functions');

df.app.orchestration('DurableFunctionsOrchestratorJS', function* (context) {
    const outputs = [];
    const city = {city:"Paris", country:"France"};

    outputs.push(yield context.df.callActivity("Hello", "Tokyo"));
    outputs.push(yield context.df.callActivity("Hello", "Seattle"));
    outputs.push(yield context.df.callActivity("Hello", "London"));
    outputs.push(yield context.df.callActivity("Hello", 123));
    outputs.push(yield context.df.callActivity("PrintArray", ["Dubai", "New York", "Vancouver"]));
    outputs.push(yield context.df.callActivity("PrintObject", city));

    return outputs;
});

df.app.activity("Hello", {
    handler: (input) => {
        return `Hello ${input}!`;
    },
});

df.app.activity("PrintArray", {
    handler: (input) => {
        return input.toString();
    },
});

df.app.activity("PrintObject", {
    handler: (input) => {
        return JSON.stringify(input);
    },
});

app.http('DurableFunctionsHttpStart', {
    route: 'DurableFunctionsHttpStart',
    extraInputs: [df.input.durableClient()],
    handler: async (request, context) => {
        const client = df.getClient(context);
        const body = await request.text();

        // Get instanceId from query parameters if provided
        const url = new URL(request.url);
        const providedInstanceId = url.searchParams.get('instanceId') || undefined;

        const instanceId = await client.startNew(
            "DurableFunctionsOrchestratorJS",
            {
                instanceId: providedInstanceId,
                input: body
            }
        );
        
        context.log(`Started orchestration with ID = '${instanceId}'.`);

        return client.createCheckStatusResponse(request, instanceId);
    },
});

module.exports = app;
