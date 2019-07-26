using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;

public static class CopyFileToBlob
{
    [FunctionName("E2_CopyFileToBlob")]
    public static async Task<long> Run(
        [ActivityTrigger] string filePath,
        Binder binder,
        ILogger log)
    {
        long byteCount = new FileInfo(filePath).Length;

        // strip the drive letter prefix and convert to forward slashes
        string blobPath = filePath
            .Substring(Path.GetPathRoot(filePath).Length)
            .Replace('\\', '/');
        string outputLocation = $"backups/{blobPath}";

        log.LogInformation($"Copying '{filePath}' to '{outputLocation}'. Total bytes = {byteCount}.");

        // copy the file contents into a blob
        using (Stream source = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (Stream destination = await binder.BindAsync<CloudBlobStream>(
            new BlobAttribute(outputLocation)))
        {
            await source.CopyToAsync(destination);
        }

        return byteCount;
    }
}
