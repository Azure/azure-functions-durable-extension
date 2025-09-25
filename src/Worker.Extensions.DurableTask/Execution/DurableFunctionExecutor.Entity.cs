// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.DurableTask.Entities;
using Microsoft.DurableTask.Worker;
using Microsoft.DurableTask.Worker.Grpc;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution;

internal partial class DurableFunctionExecutor
{
    // Must point to a PUBLIC method.
    // Functions runtime will validate this, even though it is never called.
    public static readonly string EntityEntryPoint =
        $"{typeof(DurableFunctionExecutor).FullName}.{nameof(Entity)}";

    public void Entity()
    {
        throw new NotImplementedException(
            "Do not call this method. It is a placeholder for entity function metadata.");
    }

    private async ValueTask RunEntityAsync(FunctionContext context, BindingMetadata triggerBinding)
    {
        InputBindingData<object> triggerInputData = await context.BindInputAsync<object>(triggerBinding);
        if (triggerInputData?.Value is not string encodedEntityBatch)
        {
            throw new InvalidOperationException(
                "Entity batch was either missing from the input or not a string value.");
        }

        if (context.FunctionDefinition.EntryPoint == EntityEntryPoint)
        {
            await this.RunDirectEntityAsync(context, encodedEntityBatch);
            return;
        }

        TaskEntityDispatcher dispatcher = new(encodedEntityBatch, context.InstanceServices);
        triggerInputData.Value = dispatcher;
        await inner.ExecuteAsync(context);
        context.GetInvocationResult().Value = dispatcher.Result;
    }

    private async Task RunDirectEntityAsync(
        FunctionContext context, string encodedEntityBatch)
    {
        if (factory is not IDurableTaskFactory2 factory2)
        {
            throw new InvalidOperationException(
                "The registered durable task factory does not support entity invocations.");
        }

        if (!factory2.TryCreateEntity(
            context.FunctionDefinition.Name, context.InstanceServices, out ITaskEntity? entity))
        {
            throw new InvalidOperationException(
                $"No entity with name '{context.FunctionDefinition.Name}' is registered.");
        }

        string result = await GrpcEntityRunner.LoadAndRunAsync(
            encodedEntityBatch, entity, context.InstanceServices);
        context.GetInvocationResult().Value = result;
    }
}
