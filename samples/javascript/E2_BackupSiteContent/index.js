const df = require("durable-functions");

module.exports = df(function*(context){
    const rootDirectory = context.df.getInput();
    if (!rootDirectory) {
        throw new Error("A directory path is required as an input.");
    }

    const files = yield context.df.callActivityAsync("E2_GetFileList", rootDirectory);

    // Backup Files and save Promises into array
    const tasks = [];
    for (const file of files) {
        tasks.push(context.df.callActivityAsync("E2_CopyFileToBlob", file));
    }

    // wait for all the Backup Files Activities to complete, sum total bytes
    const results = yield context.df.Task.all(tasks);
    const totalBytes = results.reduce((prev, curr) => prev + curr, 0);

    // return results;
    return totalBytes;
});