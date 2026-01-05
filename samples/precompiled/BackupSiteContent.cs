// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

/* This sample demonstrates the Fan-out/Fan-in pattern. In this pattern, multiple activity
 * functions are executed in parallel, and then the orchestrator waits for all of them to
 * complete before continuing. This is useful when you need to perform the same operation
 * on multiple items concurrently, such as backing up multiple files.
 *
 * Pattern documentation:
 * https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-cloud-backup
 *
 * To run this sample:
 *   1. Configure the AzureWebJobsStorage connection string in local.settings.json
 *   2. Start the function app locally using `func host start` or run from Visual Studio
 *   3. Make an HTTP POST request to: http://localhost:7071/orchestrators/E2_BackupSiteContent
 *      Optionally include a JSON body with the root directory path to backup
 *
 * Required app settings:
 *   - AzureWebJobsStorage: Azure Storage connection string for storing backup blobs
 */
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;

namespace VSSample
{
    public static class BackupSiteContent
    {
        // Orchestrator function that demonstrates Fan-out/Fan-in by backing up files in parallel.
        // First gets the file list (single activity), then fans out to copy each file concurrently.
        [FunctionName("E2_BackupSiteContent")]
        public static async Task<long> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext backupContext)
        {
            // Get the root directory from input, or default to the assembly directory
            string rootDirectory = backupContext.GetInput<string>()?.Trim();
            if (string.IsNullOrEmpty(rootDirectory))
            {
                rootDirectory = Directory.GetParent(typeof(BackupSiteContent).Assembly.Location).FullName;
            }

            // Step 1: Get the list of files to backup (single activity call)
            string[] files = await backupContext.CallActivityAsync<string[]>(
                "E2_GetFileList",
                rootDirectory);

            // Step 2: Fan-out - start all file copy tasks in parallel (no await yet)
            var tasks = new Task<long>[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                tasks[i] = backupContext.CallActivityAsync<long>(
                    "E2_CopyFileToBlob",
                    files[i]);
            }

            // Step 3: Fan-in - wait for all parallel tasks to complete
            await Task.WhenAll(tasks);

            // Aggregate results from all tasks
            long totalBytes = tasks.Sum(t => t.Result);
            return totalBytes;
        }

        // Activity function that retrieves the list of files from a directory.
        [FunctionName("E2_GetFileList")]
        public static string[] GetFileList(
            [ActivityTrigger] string rootDirectory, 
            ILogger log)
        {
            log.LogInformation($"Searching for files under '{rootDirectory}'...");
            string[] files = Directory.GetFiles(rootDirectory, "*", SearchOption.AllDirectories);
            log.LogInformation($"Found {files.Length} file(s) under {rootDirectory}.");

            return files;
        }

        // Activity function that copies a single file to Azure Blob Storage.
        // This function is called in parallel for each file (fan-out).
        [FunctionName("E2_CopyFileToBlob")]
        public static async Task<long> CopyFileToBlob(
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
            using (Stream source = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (Stream destination = await binder.BindAsync<CloudBlobStream>(
                new BlobAttribute(outputLocation, FileAccess.Write)))
            {
                await source.CopyToAsync(destination);
            }

            return byteCount;
        }
    }
}
