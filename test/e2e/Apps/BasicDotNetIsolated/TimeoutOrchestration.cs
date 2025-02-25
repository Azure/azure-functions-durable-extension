// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Durable.Tests.E2E;

public static class TimeoutOrchestration
{
    [Function("TimeoutOrchestrator_HttpStart")]
    public static async Task<HttpResponseData> TimerHttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext, 
        int timeoutSeconds)
    {
        ILogger logger = executionContext.GetLogger("TimeoutOrchestrator_HttpStart");

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(TimeoutOrchestrator), timeoutSeconds);

        logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }

    [Function(nameof(TimeoutOrchestrator))]
    public static async Task<string> TimeoutOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context, 
        int timeoutSeconds)
    {
        TimeSpan timeout = TimeSpan.FromSeconds(timeoutSeconds);
        DateTime deadline = context.CurrentUtcDateTime.Add(timeout);

        using (var cts = new CancellationTokenSource())
        {
            Task<string> activityTask = context.CallActivityAsync<string>(nameof(LongActivity));
            Task timeoutTask = context.CreateTimer(deadline, cts.Token);

            Task winner = await Task.WhenAny(activityTask, timeoutTask);
            if (winner == activityTask)
            {
                // success case
                cts.Cancel();
                return activityTask.Result;
            }
            else
            {
                return "The activity function timed out";
            }
        }
    }

    [Function(nameof(LongActivity))]
    public static string LongActivity([ActivityTrigger] string instanceId, FunctionContext executionContext)
    {
        Thread.Sleep(5000);
        return "The activity function completed successfully";
    }
}
