// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Entities;

using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Durable.Tests.E2E;

public class EntityCreatesScheduledOrchestration
{
    [Function("EntityCreatesScheduledOrchestrationOrchestrator_HttpStart")]
    public static async Task<HttpResponseData> HttpStartScheduled(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext,
        int scheduledStartDelaySeconds)
    {
        ILogger logger = executionContext.GetLogger("EntityCreatesScheduledOrchestrationOrchestrator_HttpStart");

        // Function input comes from the request content.
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(EntityCreatesScheduledOrchestrationOrchestrator), input: scheduledStartDelaySeconds);

        logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

        // Returns an HTTP 202 response with an instance management payload.
        // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }

    [Function(nameof(EntityCreatesScheduledOrchestrationOrchestrator))]
    public static async Task<string> EntityCreatesScheduledOrchestrationOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var entityId = new EntityInstanceId(nameof(SubOrchestratorTriggerEntity), "singleton");
        var scheduledOrchestrationInstanceId = await context.Entities.CallEntityAsync<string>(entityId, nameof(SubOrchestratorTriggerEntity.Call), context.GetInput<int>());
        return scheduledOrchestrationInstanceId;
    }

    [Function(nameof(ScheduledOrchestrationSubOrchestrator))]
    public static string ScheduledOrchestrationSubOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context, string? _)
    {
        return "Success";
    }
}


public class SubOrchestratorTriggerEntity: TaskEntity<string>
{
    public string Call(int delaySeconds)
    {
        var options = new StartOrchestrationOptions(null, DateTime.UtcNow.AddSeconds(delaySeconds));
        var instanceId = this.Context.ScheduleNewOrchestration(nameof(EntityCreatesScheduledOrchestration.ScheduledOrchestrationSubOrchestrator), null, options);
        return instanceId;
    }

    protected override string InitializeState(TaskEntityOperation entityOperation)
    {
        return string.Empty;
    }
    
    [Function(nameof(SubOrchestratorTriggerEntity))]
    public Task RunEntityAsync([EntityTrigger] TaskEntityDispatcher dispatcher)
    {
        return dispatcher.DispatchAsync(this);
    }
}