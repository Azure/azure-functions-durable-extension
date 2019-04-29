#r "Microsoft.Azure.WebJobs.Extensions.DurableTask"
#r "Microsoft.Azure.WebJobs.Extensions.Twilio"
#r "Twilio"

using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

public static void Run(string phoneNumber, out CreateMessageOptions message)
{
    message = new CreateMessageOptions(new PhoneNumber(phoneNumber));
    message.Body = $"The weather's clear outside! Go take a walk!";
}