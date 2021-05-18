const df = require("durable-functions");

module.exports = df.orchestrator(function* (context) {
    const work = [];
    const numActivities = context.df.getInput();

    for(var i = 0; i < numActivities; i++){
        work.push(context.df.callActivity("Hello", i.toString()));
    }

    yield context.df.Task.all(work)
    return "Done!";
});