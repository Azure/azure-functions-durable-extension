// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace VSSample

open System
open System.Threading
open System.Threading.Tasks
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.DurableTask
open Microsoft.Extensions.Logging
open Twilio.Rest.Api.V2010.Account
open Twilio.Types
open FSharp.Control.Tasks

module PhoneVerification =

  [<FunctionName("E4_SmsPhoneVerification")>]
  let Run([<OrchestrationTrigger>] context: IDurableOrchestrationContext) = task {
    let phoneNumber = context.GetInput<string>()
    if String.IsNullOrEmpty(phoneNumber)
    then raise (ArgumentNullException("phoneNumber", "A phone number input is required."))

    let! challengeCode = context.CallActivityAsync<int>("E4_SendSmsChallenge", phoneNumber)
    use timeoutCts = new CancellationTokenSource()

    // The user has 90 seconds to respond with the code they received in the SMS message.
    let expiration = context.CurrentUtcDateTime.AddSeconds 90.
    let timeoutTask = context.CreateTimer(expiration, timeoutCts.Token)

    let rec challenge i = task {
      let challengeResponseTask = context.WaitForExternalEvent<int>("SmsChallengeResponse")
      let! winner = Task.WhenAny(challengeResponseTask, timeoutTask)
      if (winner = timeoutTask) then return false
      elif (challengeResponseTask.Result = challengeCode) then return true
      elif i >= 0 then return! challenge (i - 1)
      else return false
    }

    let! authorized = challenge 3

    // All pending timers must be complete or canceled before the function exits.
    if (not timeoutTask.IsCompleted) then timeoutCts.Cancel()

    return authorized
  }

  [<FunctionName("E4_SendSmsChallenge")>]
  let SendSmsChallenge
    ([<ActivityTrigger>] phoneNumber: string, 
     log: ILogger,
     [<TwilioSms(AccountSidSetting = "TwilioAccountSid", AuthTokenSetting = "TwilioAuthToken", From = "%TwilioPhoneNumber%")>] messageCollector: ICollector<CreateMessageOptions>
    ) =
    // Get a random number generator with a random seed (not time-based)
    let rand = Random(Guid.NewGuid().GetHashCode())
    let challengeCode = rand.Next(10000)

    log.LogInformation (sprintf "Sending verification code %i to %s." challengeCode phoneNumber)
    let message = CreateMessageOptions(PhoneNumber(phoneNumber))
    message.Body <- sprintf "Your verification code is %i:0000" challengeCode
    messageCollector.Add message 

    challengeCode