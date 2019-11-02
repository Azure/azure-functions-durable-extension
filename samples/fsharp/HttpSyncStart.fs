// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace VSSample

open System
open System.Net.Http
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.DurableTask
open Microsoft.Azure.WebJobs.Extensions.Http
open Microsoft.Extensions.Logging
open FSharp.Control.Tasks

module HttpSyncStart = 

  let private getTimeSpan (request: HttpRequestMessage) (queryParameterName: string) (defaultSeconds: double) =
    request.RequestUri.ParseQueryString().[queryParameterName]
    |> Option.ofObj
    |> Option.map Double.Parse
    |> Option.defaultValue defaultSeconds
    |> TimeSpan.FromSeconds

  [<FunctionName("HttpSyncStart")>]
  let Run([<HttpTrigger(AuthorizationLevel.Function, "post", Route = "orchestrators/{functionName}/wait")>] req: HttpRequestMessage,
          [<DurableClient>] starter: IDurableOrchestrationClient,
          functionName: string,
          log: ILogger) =
    task {
      let! eventData =  req.Content.ReadAsAsync<Object>()
      let! instanceId = starter.StartNewAsync(functionName, eventData)

      log.LogInformation(sprintf "Started orchestration with ID = '{%s}'." instanceId)

      let timeout = getTimeSpan req "timeout" 30.0
      let retryInterval = getTimeSpan req "retryInterval" 1.0

      return! starter.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId, timeout, retryInterval)
    }