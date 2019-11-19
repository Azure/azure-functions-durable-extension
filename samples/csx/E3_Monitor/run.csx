#r "Microsoft.Azure.WebJobs.Extensions.DurableTask"
#r "Microsoft.Extensions.Logging"

#load "..\shared\MonitorRequest.csx"

using System.Threading;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

public static async Task Run(IDurableOrchestrationContext monitorContext, ILogger log)
{
    // replay-safe logger that does not log during replay (Durable Functions 2.0.0 or higher)
    log = context.CreateReplaySafeLogger(log);

    MonitorRequest input = monitorContext.GetInput<MonitorRequest>();
    log.LogInformation($"Received monitor request. Location: {input?.Location}. Phone: {input?.Phone}.");

    VerifyRequest(input);

    DateTime endTime = monitorContext.CurrentUtcDateTime.AddHours(6);
    log.LogInformation($"Instantiating monitor for {input.Location}. Expires: {endTime}.");

    while (monitorContext.CurrentUtcDateTime < endTime)
    {
        // Check the weather
        log.LogInformation($"Checking current weather conditions for {input.Location} at {monitorContext.CurrentUtcDateTime}.");

        bool isClear = await monitorContext.CallActivityAsync<bool>("E3_GetIsClear", input.Location);

        if (isClear)
        {
            // It's not raining! Or snowing. Or misting. Tell our user to take advantage of it.
            log.LogInformation($"Detected clear weather for {input.Location}. Notifying {input.Phone}.");

            await monitorContext.CallActivityAsync("E3_SendGoodWeatherAlert", input.Phone);
            break;
        }
        else
        {
            // Wait for the next checkpoint
            var nextCheckpoint = monitorContext.CurrentUtcDateTime.AddMinutes(30);
            log.LogInformation($"Next check for {input.Location} at {nextCheckpoint}.");

            await monitorContext.CreateTimer(nextCheckpoint, CancellationToken.None);
        }
    }

    log.LogInformation("Monitor expiring.");
}

private static void VerifyRequest(MonitorRequest request)
{
    if (request == null)
    {
        throw new ArgumentNullException(nameof(request), "An input object is required.");
    }

    if (request.Location == null)
    {
        throw new ArgumentNullException(nameof(request.Location), "A location input is required.");
    }

    if (string.IsNullOrEmpty(request.Phone))
    {
        throw new ArgumentNullException(nameof(request.Phone), "A phone number input is required.");
    }
}