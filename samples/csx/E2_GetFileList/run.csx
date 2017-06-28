#r "Microsoft.Azure.WebJobs.Extensions.DurableTask"

public static string[] Run(DurableActivityContext getFileListContext, TraceWriter log)
{
    string rootDirectory = getFileListContext.GetInput<string>();
    string[] files = Directory.GetFiles(rootDirectory, "*", SearchOption.AllDirectories);
    log.Info($"Found {files.Length} file(s) under {rootDirectory}.");

    return files;
}