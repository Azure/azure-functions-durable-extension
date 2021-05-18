const df = require("durable-functions");
const validateInput = require("../SharedUtils/validateInput")
const pLimit = require('p-limit');

module.exports = async function (context, req) {
    const client = df.getClient(context);
    const numInstances = req.body;

    validateInput(numInstances);
    var orchestratorStarts = new Array(numInstances).fill(client.startNew("SequentialOrchestration"));

    // concurrency limit to 200 async threads
    pLimit(200);
    await Promise.all(orchestratorStarts);
};