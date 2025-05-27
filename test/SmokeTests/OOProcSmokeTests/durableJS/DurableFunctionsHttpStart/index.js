const df = require("durable-functions");

module.exports = async function (context, req) {
    const client = df.getClient(context);
    const body = await req.text();
    const instanceId = await client.startNew("DurableFunctionsOrchestratorJS", { input: body });

    context.log(`Started orchestration with ID = '${instanceId}'.`);

    return client.createCheckStatusResponse(context.bindingData.req, instanceId);
};