const df = require("durable-functions");

module.exports = async function (context, req) {
    const client = df.getClient(context);
    const numActivities = req.body;
    const instanceId = await client.startNew("FanOutFanInOrchestration", undefined, numActivities);

    context.log(`Started orchestration with ID = '${instanceId}'.`);

    return client.createCheckStatusResponse(context.bindingData.req, instanceId);
};