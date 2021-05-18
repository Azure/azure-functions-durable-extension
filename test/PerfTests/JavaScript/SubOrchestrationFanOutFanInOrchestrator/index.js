const df = require("durable-functions");
const validateInput = require("../SharedUtils/validateInput")

module.exports = df.orchestrator(function* (context) {
    const outputs = [];
    const numSubOrchestrators = context.df.getInput();

    validateInput(numSubOrchestrators);

    yield context.df.Task.all(outputs);
    return "Done!";
});