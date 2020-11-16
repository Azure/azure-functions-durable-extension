using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace FunctionApp14
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