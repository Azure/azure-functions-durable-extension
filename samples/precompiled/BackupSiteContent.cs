// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Blob;

namespace VSSample
{
    public static class BackupSiteContent
    {
        [FunctionName("E2_BackupSiteContent")]
        public static async Task<long> Run(
            [OrchestrationTrigger] DurableOrchestrationContext backupContext)
        {
            string rootDirectory = backupContext.GetInput<string>();
            if (string.IsNullOrEmpty(rootDirectory))
            {
                throw new ArgumentNullException(nameof(rootDirectory), "A root directory must be specified");
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

        [FunctionName("E2_GetFileList")]
        public static string[] GetFileList(
            [ActivityTrigger] DurableActivityContext getFileListContext,
            TraceWriter log)
        {
            string rootDirectory = getFileListContext.GetInput<string>();
            log.Info($"Searching for files under {rootDirectory}...");
            string[] files = Directory.GetFiles(rootDirectory, "*", SearchOption.AllDirectories);
            log.Info($"Found {files.Length} file(s) under {rootDirectory}.");

            return files;
        }

        [FunctionName("E2_CopyFileToBlob")]
        public static async Task<long> CopyFileToBlob(
            [ActivityTrigger] DurableActivityContext copyFileContext,
            Binder binder,
            TraceWriter log)
        {
            string filePath = copyFileContext.GetInput<string>();
            long byteCount = new FileInfo(filePath).Length;

            // strip the drive letter prefix and convert to forward slashes
            string blobPath = filePath
                .Substring(Path.GetPathRoot(filePath).Length)
                .Replace('\\', '/');
            string outputLocation = $"backups/{blobPath}";

            log.Info($"Copying '{filePath}' to '{outputLocation}'. Total bytes = {byteCount}.");

            // copy the file contents into a blob
            using (Stream source = File.Open(filePath, FileMode.Open))
            using (Stream destination = await binder.BindAsync<CloudBlobStream>(
                new BlobAttribute(outputLocation)))
            {
                await source.CopyToAsync(destination);
            }

            return byteCount;
        }
    }
}
