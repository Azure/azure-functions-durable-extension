const df = require("durable-functions");

// Fan-out/fan-in refers to the pattern of executing multiple functions in parallel, and then waiting for all to finish.
// This sample demonstates a cloud backup. In parrallel, it copies all files in a root directory to blobs, returning the total number of bytes copied.
// More on running this sample here: https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-cloud-backup 

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