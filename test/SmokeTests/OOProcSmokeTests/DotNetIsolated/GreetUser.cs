// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace DotNetIsolated;

public static class GreetUser
{
    [Function(nameof(GreetUser))]
    public static async Task<HttpResponseData> Start(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(GreetUser));

        SayHelloPayload? payload = await req.ReadFromJsonAsync<SayHelloPayload>();
        string instanceId = await client
            .ScheduleNewOrchestrationInstanceAsync(nameof(GreetUserOrchestration), payload);
        logger.LogInformation("Created new orchestration with instance ID = {instanceId}", instanceId);

        return client.CreateCheckStatusResponse(req, instanceId);
    }

    [Function(nameof(GreetUserOrchestration))]
    public static async Task<SayHelloPayload> GreetUserOrchestration([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        SayHelloPayload? input = context.GetInput<SayHelloPayload>();
        SayHelloPayload result = await context.CallActivityAsync<SayHelloPayload>(nameof(GreetUserActivity), input);
        return result;
    }

    [Function(nameof(GreetUserActivity))]
    public static SayHelloPayload GreetUserActivity([ActivityTrigger] SayHelloPayload input, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(GreetUserActivity));
        logger.LogInformation("Saying hello to {input}", input);
        return input;
    }

    public record SayHelloPayload(string First, string Last, int Age);
}
