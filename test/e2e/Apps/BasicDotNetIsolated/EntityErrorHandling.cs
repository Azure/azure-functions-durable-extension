// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Entities;

namespace Microsoft.Azure.Durable.Tests.E2E;

public static class EntityErrorHandling
{
    private static ConcurrentDictionary<string, int> retryCount = new ConcurrentDictionary<string, int>();

    [Function(nameof(ThrowEntityOrchestration))]
    public static async Task<string> ThrowEntityOrchestration([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var entityId = new EntityInstanceId(nameof(Counter), "MyExceptionEntity");

        int entityResult = await context.Entities.CallEntityAsync<int>(entityId, "ThrowFirstTimeOnly", context.InstanceId);
        return "Success";
    }

    [Function(nameof(CatchEntityOrchestration))]
    public static async Task<string> CatchEntityOrchestration([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var entityId = new EntityInstanceId(nameof(Counter), "MyExceptionEntity");

        try
        {
            int entityResult = await context.Entities.CallEntityAsync<int>(entityId, "ThrowFirstTimeOnly", context.InstanceId);
            return "Success";
        }
        // This is interesting - activities, when thrown, raise the native exception type. Entities, however, always raise
        // EntityOperationFailedException
        catch (EntityOperationFailedException ex) 
        {
            return ex.Message;
        }
    }

    [Function(nameof(RetryEntityOrchestration))]
    public static async Task<string> RetryEntityOrchestration([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var entityId = new EntityInstanceId(nameof(Counter), "MyExceptionEntity");

        try
        {
            int entityResult = await context.Entities.CallEntityAsync<int>(entityId, "ThrowFirstTimeOnly", context.InstanceId);
            return "Success";
        }
        catch (EntityOperationFailedException)
        {
            int entityResult = await context.Entities.CallEntityAsync<int>(entityId, "ThrowFirstTimeOnly", context.InstanceId);
            return "Success";
        }
    }

    [Function(nameof(Counter))]
    public static Task Counter([EntityTrigger] TaskEntityDispatcher dispatcher)
    {
        return dispatcher.DispatchAsync(operation =>
        {
            string? instanceId = operation.GetInput<string>();
            if (instanceId is null)
            {
                throw new ArgumentException("Did not get a valid instanceId as input to the entity");
            }

            // Entity logic would go here - this entity does nothing

            if (retryCount.AddOrUpdate(instanceId, 1, (key, oldValue) => oldValue + 1) == 1)
            {
                var exception = new InvalidOperationException("This entity failed\r\nMore information about the failure", innerException: new OverflowException("Inner exception message"));
                throw exception;
            }
            return new(0);
        });
    }
}
