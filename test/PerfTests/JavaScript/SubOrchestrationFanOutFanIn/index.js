const df = require("durable-functions");
const validateInput = require("../SharedUtils/validateInput")

module.exports = async function (context, req) {
    const client = df.getClient(context);
    const numSubOrchestrators = req.body;
    validateInput(numSubOrchestrators);

    const instanceId = await client.startNew("SubOrchestrationFanOutFanInOrchestrator", undefined, numSubOrchestrators);

    context.log(`Started orchestration with ID = '${instanceId}'.`);

    return client.createCheckStatusResponse(context.bindingData.req, instanceId);
};