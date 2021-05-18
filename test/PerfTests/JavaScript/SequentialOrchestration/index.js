const df = require("durable-functions");

module.exports = df.orchestrator(function* (context) {
    const outputs = [];

    for(var i = 0; i < 100; i++){
        yield context.df.callActivity("Hello", i.toString());
    }
    return "Done!";
});