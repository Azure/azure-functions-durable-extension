#r "Microsoft.Azure.WebJobs.Extensions.DurableTask"

public static async Task<long> Run(DurableOrchestrationContext backupContext)
{
    string rootDirectory = backupContext.GetInput<string>();
    if (string.IsNullOrEmpty(rootDirectory))
    {
        rootDirectory = Environment.CurrentDirectory;
    }

    string[] files = await backupContext.CallFunctionAsync<string[]>(
        "E2_GetFileList",
        rootDirectory);

    var tasks = new Task<long>[files.Length];
    for (int i = 0; i < files.Length; i++)
    {
        tasks[i] = backupContext.CallFunctionAsync<long>(
            "E2_CopyFileToBlob",
            files[i]);
    }

    await Task.WhenAll(tasks);

    long totalBytes = tasks.Sum(t => t.Result);
    return totalBytes;
}