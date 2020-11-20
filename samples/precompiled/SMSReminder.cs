using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

/* This sample is meant to be used with the To Do List sample at
 * https://github.com/Azure/azure-functions-durable-extension/tree/dev/samples/todolist-aspnetcore
 * 
 * To run this sample, you'll need to define the following app settings:
 *
 *   - TwilioAccountSid: your Twilio account's SID
 *   - TwilioAuthToken: your Twilio account's auth token
 *   - TwilioPhoneNumber: an SMS-capable Twilio number
 *   - TwilioPhoneNumberTo: an SMS-capable phone number to send the text to
 *
 * Twilio: https://www.twilio.com
 */

namespace VSSample
{
    public static class SMSReminder
    {
        [FunctionName("SetReminder")]
        public static async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            string task = context.GetInput<string>();

            DateTime fireAt = context.CurrentUtcDateTime.AddHours(24);
            await context.CreateTimer(fireAt, CancellationToken.None);

            await context.CallActivityAsync("SendSMS", task);

        }

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