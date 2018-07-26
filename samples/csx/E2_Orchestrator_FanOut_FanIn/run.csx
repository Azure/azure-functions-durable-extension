#r "Microsoft.Azure.WebJobs.Extensions.DurableTask"

// Fan-out/fan-in refers to the pattern of executing multiple functions in parallel, and then waiting for all to finish.
// This sample demonstates a cloud backup. In parrallel, it copies all files in a root directory to blobs, returning the total number of bytes copied.
// More on running this sample here: https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-cloud-backup 

public static async Task<long> Run(DurableOrchestrationContext backupContext)
{
    string rootDirectory = Environment.ExpandEnvironmentVariables(backupContext.GetInput<string>() ?? "");
    if (string.IsNullOrEmpty(rootDirectory))
    {
        throw new ArgumentException("A directory path is required as an input.");
    }

    if (!Directory.Exists(rootDirectory))
    {
        throw new DirectoryNotFoundException($"Could not find a directory named '{rootDirectory}'.");
    }

    string[] files = await backupContext.CallActivityAsync<string[]>(
        "E2_GetFileList",
        rootDirectory);

    // schedule all files to be copied to blobs
    var tasks = new Task<long>[files.Length];
    for (int i = 0; i < files.Length; i++)
    {
        tasks[i] = backupContext.CallActivityAsync<long>(
            "E2_CopyFileToBlob",
            files[i]);
    }

    // wait for all parallel tasks to complete
    await Task.WhenAll(tasks);

    long totalBytes = tasks.Sum(t => t.Result);
    return totalBytes;
}