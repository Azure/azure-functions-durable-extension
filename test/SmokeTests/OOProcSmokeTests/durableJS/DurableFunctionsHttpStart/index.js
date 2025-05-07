const df = require("durable-functions");

module.exports = async function (context, req, starter) {
    const instanceId = await starter.startNew("DurableFunctionsOrchestratorJS", {
        input: req.body
      });

    context.log(`Started orchestration with ID = '${instanceId}'.`);

    return starter.createCheckStatusResponse(context.bindingData.req, instanceId);
};