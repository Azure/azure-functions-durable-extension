// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace VSSample

open System
open System.IO
open System.Threading.Tasks
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Host
open Microsoft.WindowsAzure.Storage.Blob
open FSharp.Control.Tasks

module BackupSiteContent =

  [<FunctionName("E2_BackupSiteContent")>]
  let Run([<OrchestrationTrigger>] backupContext: DurableOrchestrationContext) = task {
    let input = backupContext.GetInput<string>()
    let rootDirectory = 
      if String.IsNullOrEmpty(input) then input.Trim()
      else Directory.GetParent(System.Reflection.Assembly.GetExecutingAssembly().Location).FullName

    let! files = backupContext.CallActivityAsync<string[]>("E2_GetFileList", rootDirectory)
    let tasks = files |> Array.map (fun f -> backupContext.CallActivityAsync<int64>("E2_CopyFileToBlob", f))
    let! results = Task.WhenAll tasks

    let totalBytes = Array.sum results
    return totalBytes
  }
  
  [<FunctionName("E2_GetFileList")>]
  let GetFileList([<ActivityTrigger>] rootDirectory: string, log: TraceWriter) =
    log.Info (sprintf "Searching for files under '%s'..." rootDirectory)
    let files = Directory.GetFiles(rootDirectory, "*", SearchOption.AllDirectories)
    log.Info (sprintf "Found %i file(s) under %s." files.Length rootDirectory)
    files

  [<FunctionName("E2_CopyFileToBlob")>]
  let CopyFileToBlob([<ActivityTrigger>] filePath: string, binder: Binder, log: TraceWriter) = task {
    let byteCount = FileInfo(filePath).Length

    // strip the drive letter prefix and convert to forward slashes
    let blobPath = filePath.Substring(Path.GetPathRoot(filePath).Length).Replace('\\', '/')
    let outputLocation = "backups/" + blobPath

    log.Info (sprintf "Copying '%s' to '%s'. Total bytes = %i." filePath outputLocation byteCount)

    // copy the file contents into a blob
    use source = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)
    let attribute = BlobAttribute(outputLocation, FileAccess.Write)
    use! destination = binder.BindAsync<CloudBlobStream> attribute
    do! source.CopyToAsync destination

    return byteCount
  }