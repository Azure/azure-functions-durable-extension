#r "Microsoft.Azure.WebJobs.Extensions.DurableTask"

#load "..\shared\MonitorRequest.csx"

using System.Threading;

public static async Task Run(
    DurableOrchestrationContext monitorContext, 
    TraceWriter log)
{
    MonitorRequest input = monitorContext.GetInput<MonitorRequest>();
    if (!monitorContext.IsReplaying) { log.Info(string.Format(
        "Received monitor request. Location: {0}. Phone: {1}.", 
        input?.Location, 
        input?.Phone)); }

    VerifyRequest(input);

    DateTime endTime = monitorContext.CurrentUtcDateTime.AddHours(6);
    if (!monitorContext.IsReplaying) { log.Info(string.Format(
        "Instantiating monitor for {0}. Expires: {1}.",
        input.Location,
        endTime)); }

    while (monitorContext.CurrentUtcDateTime < endTime)
    {
        // Check the weather
        if (!monitorContext.IsReplaying) { log.Info(string.Format(
            "Checking current weather conditions for {0} at {1}.",
            input.Location,
            monitorContext.CurrentUtcDateTime)); }

        bool isClear = await monitorContext.CallActivityAsync<bool>(
            "E3_GetIsClear", 
            input.Location);

        if (isClear)
        {
            // It's not raining! Or snowing. Or misting.
            // Tell our user to take advantage of it.
            if (!monitorContext.IsReplaying) { log.Info(string.Format(
                "Detected clear weather for {0}. Notifying {1}.",
                input.Location,
                input.Phone)); }

            await monitorContext.CallActivityAsync(
                "E3_SendGoodWeatherAlert", 
                input.Phone);
            break;
        }
        else
        {
            // Wait for the next checkpoint
            var nextCheckpoint = monitorContext.CurrentUtcDateTime.AddMinutes(30);
            if (!monitorContext.IsReplaying) { log.Info(string.Format(
                "Next check for {0} at {1}.",
                input.Location,
                nextCheckpoint)); }

            await monitorContext.CreateTimer(
                nextCheckpoint, 
                CancellationToken.None);
        }
    }

    log.Info($"Monitor expiring.");
}

private static void VerifyRequest(MonitorRequest request)
{
    if (request == null)
    {
        throw new ArgumentNullException(
            nameof(request), 
            "An input object is required.");
    }

    if (request.Location == null)
    {
        throw new ArgumentNullException(
            nameof(request.Location), 
            "A location input is required.");
    }

    if (string.IsNullOrEmpty(request.Phone))
    {
        throw new ArgumentNullException(
            nameof(request.Phone), 
            "A phone number input is required.");
    }
}