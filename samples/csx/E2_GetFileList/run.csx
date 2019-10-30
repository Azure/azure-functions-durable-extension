#r "Microsoft.Azure.WebJobs.Extensions.DurableTask"
#r "Microsoft.Extensions.Logging"

public static string[] Run(string rootDirectory, ILogger log)
{
    string[] files = Directory.GetFiles(rootDirectory, "*", SearchOption.AllDirectories);
    log.LogInformation($"Found {files.Length} file(s) under {rootDirectory}.");

    return files;
}