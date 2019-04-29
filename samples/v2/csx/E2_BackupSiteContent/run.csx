#r "Microsoft.Azure.WebJobs.Extensions.DurableTask"

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

    var tasks = new Task<long>[files.Length];
    for (int i = 0; i < files.Length; i++)
    {
        tasks[i] = backupContext.CallActivityAsync<long>(
            "E2_CopyFileToBlob",
            files[i]);
    }

    await Task.WhenAll(tasks);

    long totalBytes = tasks.Sum(t => t.Result);
    return totalBytes;
}