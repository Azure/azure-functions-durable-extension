#r "Microsoft.Azure.WebJobs.Extensions.DurableTask"
#r "Microsoft.Azure.WebJobs.Extensions.Twilio"
#r "Newtonsoft.Json"
#r "Twilio"

using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

public static int Run(
    string phoneNumber,
    TraceWriter log,
    out CreateMessageOptions message)
{
    // Get a random number generator with a random seed (not time-based)
    var rand = new Random(Guid.NewGuid().GetHashCode());
    int challengeCode = rand.Next(10000);

    log.Info($"Sending verification code {challengeCode} to {phoneNumber}.");

    message = new CreateMessageOptions(new PhoneNumber(phoneNumber));
    message.Body = $"Your verification code is {challengeCode:0000}";

    return challengeCode;
}
