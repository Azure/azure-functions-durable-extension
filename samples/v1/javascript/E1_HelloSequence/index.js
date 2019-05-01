const df = require("durable-functions");

module.exports = df.orchestrator(function*(context){
    context.log("Starting chain sample");
    const output = [];
    output.push(yield context.df.callActivity("E1_SayHello", "Tokyo"));
    output.push(yield context.df.callActivity("E1_SayHello", "Seattle"));
    output.push(yield context.df.callActivity("E1_SayHello", "London"));

    return output;
});
