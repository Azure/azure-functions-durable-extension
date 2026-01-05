// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

/* This sample demonstrates the Human Interaction pattern. In this pattern, the orchestrator
 * waits for an external event (human input) before continuing. This example implements
 * SMS-based phone verification where a code is sent to the user and they must respond
 * with the correct code within a timeout period.
 *
 * Pattern documentation:
 * https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-phone-verification
 *
 * To run this sample:
 *   1. Configure the required Twilio app settings (see below)
 *   2. Start the function app locally using `func host start` or run from Visual Studio
 *   3. Make an HTTP POST request to: http://localhost:7071/orchestrators/E4_SmsPhoneVerification
 *      Include the phone number as a JSON string in the request body
 *   4. After receiving the SMS code, send an event to the orchestration:
 *      POST to the sendEventPostUri with eventName "SmsChallengeResponse" and the code as body
 *
 * Required app settings:
 *   - TwilioAccountSid: Your Twilio account's SID
 *   - TwilioAuthToken: Your Twilio account's auth token
 *   - TwilioPhoneNumber: An SMS-capable Twilio phone number
 *
 * For Twilio trial accounts, you must verify the destination phone number first.
 * Twilio: https://www.twilio.com
 */
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace VSSample
{
    public static class PhoneVerification
    {
        // Orchestrator function that waits for external human interaction (SMS verification).
        // Demonstrates timeout handling and external event correlation.
        [FunctionName("E4_SmsPhoneVerification")]
        public static async Task<bool> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            string phoneNumber = context.GetInput<string>();
            if (string.IsNullOrEmpty(phoneNumber))
            {
                throw new ArgumentNullException(
                    nameof(phoneNumber),
                    "A phone number input is required.");
            }

            // Send an SMS with a verification code to the user
            int challengeCode = await context.CallActivityAsync<int>(
                "E4_SendSmsChallenge",
                phoneNumber);

            using (var timeoutCts = new CancellationTokenSource())
            {
                // The user has 90 seconds to respond with the code they received in the SMS message.
                DateTime expiration = context.CurrentUtcDateTime.AddSeconds(90);
                Task timeoutTask = context.CreateTimer(expiration, timeoutCts.Token);

                bool authorized = false;
                // Allow up to 3 retry attempts for incorrect codes
                for (int retryCount = 0; retryCount <= 3; retryCount++)
                {
                    // Wait for the user to send the "SmsChallengeResponse" event with their code
                    Task<int> challengeResponseTask =
                        context.WaitForExternalEvent<int>("SmsChallengeResponse");

                    // Race between user response and timeout
                    Task winner = await Task.WhenAny(challengeResponseTask, timeoutTask);
                    if (winner == challengeResponseTask)
                    {
                        // We got back a response! Compare it to the challenge code.
                        if (challengeResponseTask.Result == challengeCode)
                        {
                            authorized = true;
                            break;
                        }
                        // Wrong code - continue to next retry iteration
                    }
                    else
                    {
                        // Timeout expired
                        break;
                    }
                }

                if (!timeoutTask.IsCompleted)
                {
                    // All pending timers must be complete or canceled before the function exits.
                    timeoutCts.Cancel();
                }

                return authorized;
            }
        }

        // Activity function that generates a random code and sends it via SMS using Twilio.
        // The code is returned to the orchestrator for verification against user input.
        [FunctionName("E4_SendSmsChallenge")]
        public static int SendSmsChallenge(
            [ActivityTrigger] string phoneNumber,
            ILogger log,
            [TwilioSms(AccountSidSetting = "TwilioAccountSid", AuthTokenSetting = "TwilioAuthToken", From = "%TwilioPhoneNumber%")]
                out CreateMessageOptions message)
        {
            // Get a random number generator with a random seed (not time-based)
            var rand = new Random(Guid.NewGuid().GetHashCode());
            int challengeCode = rand.Next(10000);

            log.LogInformation($"Sending verification code {challengeCode} to {phoneNumber}.");

            message = new CreateMessageOptions(new PhoneNumber(phoneNumber));
            message.Body = $"Your verification code is {challengeCode:0000}";

            return challengeCode;
        }
    }
}
