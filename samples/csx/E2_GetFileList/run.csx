#r "Microsoft.Azure.WebJobs.Extensions.DurableTask"
#r "Microsoft.Extensions.Logging"

// This is an activity called by the fanIn/fanOut pattern sample orchestrator.
public static string[] Run(string rootDirectory, ILogger log)
{
    string[] files = Directory.GetFiles(rootDirectory, "*", SearchOption.AllDirectories);
    log.LogInformation($"Found {files.Length} file(s) under {rootDirectory}.");

    // returns the files in the root directory
    return files;
}