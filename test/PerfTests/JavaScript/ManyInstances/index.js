const df = require("durable-functions");

module.exports = async function (context, req) {
    const client = df.getClient(context);
    const numInstances = req.body;

    for(var i = 0; i < numInstances; i++)
    {
        var instanceId = await client.startNew("ManyInstancesOrchestrator");
        context.log(`Started orchestration with ID = '${instanceId}'.`);
    }

    return client.createCheckStatusResponse(context.bindingData.req, instanceId);
};