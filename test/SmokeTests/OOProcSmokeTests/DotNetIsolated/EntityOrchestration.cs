// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Logging;

namespace DotNetIsolated.Typed;

/// <summary>
/// A simple entity for counting with class-based syntax, inheriting from TaskEntity.
/// </summary>
[DurableTask(nameof(CountingEntity))]
public class CountingEntity : TaskEntity<int>
{
    public void Increment(int amount)
    {
        this.State += amount;
    }

    public int Get()
    {
        return this.State;
    }
}

public static class EntityOrchestrationStarter
{
    /// <summary>
    /// HTTP-triggered function that starts the <see cref="EntityOrchestration"/> orchestration.
    /// </summary>
    [Function(nameof(StartEntityOrchestration))]
    public static async Task<HttpResponseData> StartEntityOrchestration(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(StartEntityOrchestration));

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(EntityOrchestration));
        logger.LogInformation("Created new orchestration with instance ID = {instanceId}", instanceId);

        return client.CreateCheckStatusResponse(req, instanceId);
    }
}

/// <summary>
/// Class-based orchestrator that interacts with entities and validates their state.
/// </summary>
[DurableTask(nameof(EntityOrchestration))]
public class EntityOrchestration : TaskOrchestrator<string?, string>
{
    public async override Task<string> RunAsync(TaskOrchestrationContext context, string? input)
    {
        var entityId = new EntityInstanceId(nameof(CountingEntity), "testEntity");

        // Fetch initial state (should be 0 or not exist)
        int initialState = await context.Entities.CallEntityAsync<int>(entityId, "Get");

        // Assert initial value is 0
        if (initialState != 0)
        {
            throw new InvalidOperationException($"Expected initial value to be 0, but got {initialState}");
        }

        // Increment the entity by 5
        await context.Entities.CallEntityAsync(entityId, "Increment", 5);

        // Fetch the state again
        int updatedState = await context.Entities.CallEntityAsync<int>(entityId, "Get");

        // Assert the value is now 5
        if (updatedState != 5)
        {
            throw new InvalidOperationException($"Expected value to be 5 after increment, but got {updatedState}");
        }

        // Increment again by 3
        await context.Entities.CallEntityAsync(entityId, "Increment", 3);

        // Fetch the final state
        int finalState = await context.Entities.CallEntityAsync<int>(entityId, "Get");

        // Assert the value is now 8
        if (finalState != 8)
        {
            throw new InvalidOperationException($"Expected final value to be 8, but got {finalState}");
        }

        return $"Entity test completed successfully! Final value: {finalState}";
    }
}
