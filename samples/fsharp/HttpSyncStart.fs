// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace VSSample

open System
open System.Net.Http
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http
open Microsoft.Azure.WebJobs.Host
open FSharp.Control.Tasks

module HttpSyncStart = 

  let private getTimeSpan (request: HttpRequestMessage) (queryParameterName: string) =
    request.RequestUri.ParseQueryString().[queryParameterName]
    |> Option.ofObj
    |> Option.map Double.Parse
    |> Option.map TimeSpan.FromSeconds
    |> Option.toNullable

  [<FunctionName("HttpSyncStart")>]
  let Run([<HttpTrigger(AuthorizationLevel.Function, "post", Route = "orchestrators/{functionName}/wait")>] req: HttpRequestMessage,
          [<OrchestrationClient>] starter: DurableOrchestrationClient,
          functionName: string,
          log: TraceWriter) =
    task {
      let! eventData =  req.Content.ReadAsAsync<Object>()
      let! instanceId = starter.StartNewAsync(functionName, eventData)

      log.Info(sprintf "Started orchestration with ID = '{%s}'." instanceId)

      let timeout = getTimeSpan req "timeout"
      let retryInterval = getTimeSpan req "retryInterval"

      return! starter.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId, timeout, retryInterval)
    }