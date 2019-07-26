using Microsoft.Azure.WebJobs;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

public static class SendGoodWeatherAlert
{
    [FunctionName("E3_SendGoodWeatherAlert")]
    public static CreateMessageOptions Run([ActivityTrigger] string phoneNumber)
    {
        var message = new CreateMessageOptions(new PhoneNumber(phoneNumber));
        message.Body = $"The weather's clear outside! Go take a walk!";
        return message;
    }
}
