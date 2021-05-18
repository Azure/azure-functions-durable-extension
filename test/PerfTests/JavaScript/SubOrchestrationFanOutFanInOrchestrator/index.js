const df = require("durable-functions");

module.exports = df.orchestrator(function* (context) {
    const outputs = [];
    const numSubOrchestrators = context.df.getInput();

    for(var i = 0; i < numSubOrchestrators; i++){
        outputs.push(context.df.callSubOrchestrator("ManyInstancesOrchestrator"));
    }

    yield context.df.Task.all(outputs);
    return "Done!";
});