const df = require("durable-functions");

// Function chaining refers to the pattern of executing a sequence of functions in a particular order.
// This orchestrator performs three activity functions sequentially.
// More on running this sample here: https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-sequence

module.exports = df(function*(context){
    context.log("Starting chain sample");
    const output = [];
    output.push(yield context.df.callActivityAsync("E1_SayHello", "Tokyo"));
    output.push(yield context.df.callActivityAsync("E1_SayHello", "Seattle"));
    output.push(yield context.df.callActivityAsync("E1_SayHello", "London"));

    return output;
});