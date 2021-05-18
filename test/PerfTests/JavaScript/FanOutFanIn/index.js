const df = require("durable-functions");
const validateInput = require("../SharedUtils/validateInput")

module.exports = async function (context, req) {
    const client = df.getClient(context);
    const numActivities = req.body;

    validateInput(numActivities);

    const instanceId = await client.startNew("FanOutFanInOrchestration", undefined, numActivities);

    context.log(`Started orchestration with ID = '${instanceId}'.`);

    return client.createCheckStatusResponse(context.bindingData.req, instanceId);
};