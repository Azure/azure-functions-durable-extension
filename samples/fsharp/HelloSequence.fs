// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace VSSample

open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.DurableTask
open FSharp.Control.Tasks

module HelloSequence =

  [<FunctionName("E1_HelloSequence")>]
  let Run([<OrchestrationTrigger>] context: IDurableOrchestrationContext) = task {
    let! hello1 = context.CallActivityAsync<string>("E1_SayHello", "Tokyo")
    let! hello2 = context.CallActivityAsync<string>("E1_SayHello", "Seattle")
    let! hello3 = context.CallActivityAsync<string>("E1_SayHello", "London")

    // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
    return [hello1; hello2; hello3]
  }       

  [<FunctionName("E1_SayHello")>]
  let SayHello([<ActivityTrigger>] name) =
    sprintf "Hello %s!" name

