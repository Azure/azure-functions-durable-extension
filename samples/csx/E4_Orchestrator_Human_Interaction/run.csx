#r "Microsoft.Azure.WebJobs.Extensions.DurableTask"

using System.Threading;

// The monitor pattern refers to a flexible recurring process in a workflow - for example, polling until certain conditions are met.
// This sample implements an SMS-based phone verification system using a durable timer timeout.
// More on running this sample here: https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-phone-verification
public static async Task<bool> Run(DurableOrchestrationContext context)
{
    string phoneNumber = context.GetInput<string>();
    if (string.IsNullOrEmpty(phoneNumber))
    {
        throw new ArgumentNullException(
            nameof(phoneNumber),
            "A phone number input is required.");
    }

    int challengeCode = await context.CallActivityAsync<int>(
        "E4_SendSmsChallenge",
        phoneNumber);

    using (var timeoutCts = new CancellationTokenSource())
    {
        // The user has 90 seconds to respond with the code they received in the SMS message.
        DateTime expiration = context.CurrentUtcDateTime.AddSeconds(90);
        Task timeoutTask = context.CreateTimer(expiration, timeoutCts.Token);

        bool authorized = false;
        for (int retryCount = 0; retryCount <= 3; retryCount++)
        {
            Task<int> challengeResponseTask = 
                context.WaitForExternalEvent<int>("SmsChallengeResponse");
                
            // get either a response or a timeout    
            Task winner = await Task.WhenAny(challengeResponseTask, timeoutTask);
            if (winner == challengeResponseTask)
            {
                // We got back a response! Compare it to the challenge code.
                if (challengeResponseTask.Result == challengeCode)
                {
                    authorized = true;
                    break;
                }
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