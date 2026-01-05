// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

/* This sample demonstrates durable timers for delayed execution (reminder pattern).
 * The orchestrator sets a 24-hour timer and then sends an SMS reminder when it fires.
 * This is useful for scheduling future actions like reminders, follow-ups, or notifications.
 *
 * This sample is meant to be used with the To Do List sample at:
 * https://github.com/Azure/azure-functions-durable-extension/tree/dev/samples/todolist-aspnetcore
 *
 * Pattern documentation:
 * https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-timers
 *
 * To run this sample:
 *   1. Configure the required Twilio app settings (see below)
 *   2. Start the function app locally using `func host start` or run from Visual Studio
 *   3. The orchestration is typically started by the To Do List sample when creating tasks
 *
 * Required app settings:
 *   - TwilioAccountSid: Your Twilio account's SID
 *   - TwilioAuthToken: Your Twilio account's auth token
 *   - TwilioPhoneNumber: An SMS-capable Twilio phone number
 *   - TwilioPhoneNumberTo: An SMS-capable phone number to send the reminder to
 *
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
    public static class SMSReminder
    {
        // Orchestrator function that waits 24 hours before sending an SMS reminder.
        // Demonstrates the durable timer pattern for delayed execution.
        [FunctionName("SetReminder")]
        public static async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            string task = context.GetInput<string>();

            // Schedule the reminder for 24 hours from now
            // The timer is durable - the orchestration can be unloaded and resumed when the timer fires
            DateTime fireAt = context.CurrentUtcDateTime.AddHours(24);
            await context.CreateTimer(fireAt, CancellationToken.None);

            // Send the reminder after the timer fires
            await context.CallActivityAsync("SendSMS", task);

        }

        // Activity function that sends an SMS reminder using Twilio.
        [FunctionName("SendSMS")]
        public static void SendSMS([ActivityTrigger] string taskName,
            ILogger log,
            [TwilioSms(AccountSidSetting = "TwilioAccountSid", AuthTokenSetting = "TwilioAuthToken", From = "%TwilioPhoneNumber%")]
                out CreateMessageOptions message)
        {
            string toNumber = Environment.GetEnvironmentVariable("TwilioPhoneNumberTo");
            message = new CreateMessageOptions(new PhoneNumber(toNumber));
            message.Body = $"Make sure to finish {taskName}";
        }
    }
}