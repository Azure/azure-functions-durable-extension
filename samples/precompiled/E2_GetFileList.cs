using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

public static class GetFileList
{
    [FunctionName("E2_GetFileList")]
    public static string[] Run([ActivityTrigger] string rootDirectory, ILogger log)
    {
        string[] files = Directory.GetFiles(rootDirectory, "*", SearchOption.AllDirectories);
        log.LogInformation($"Found {files.Length} file(s) under {rootDirectory}.");

        return files;
    }
}
