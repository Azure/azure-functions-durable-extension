#r "Microsoft.Azure.WebJobs.Extensions.DurableTask"

public static string[] Run(string rootDirectory, TraceWriter log)
{
    string[] files = Directory.GetFiles(rootDirectory, "*", SearchOption.AllDirectories);
    log.Info($"Found {files.Length} file(s) under {rootDirectory}.");

    return files;
}