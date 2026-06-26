// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Google.Protobuf;
using Microsoft.DurableTask.Entities;
using Microsoft.DurableTask.Worker;
using Microsoft.DurableTask.Worker.Grpc;
using P = Microsoft.DurableTask.Protobuf;

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

        TaskEntityDispatcher dispatcher = new(encodedEntityBatch, context.InstanceServices, extendedSessionsCache);
        triggerInputData.Value = dispatcher;
        await inner.ExecuteAsync(context);

        string entityResult = dispatcher.Result;

        if (this.IsWorkerDraining)
        {
            entityResult = FlagEntityDraining(entityResult);
        }

        context.GetInvocationResult().Value = entityResult;
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

        if (this.IsWorkerDraining)
        {
            result = FlagEntityDraining(result);
        }

        context.GetInvocationResult().Value = result;
    }

    private static string FlagEntityDraining(string encodedEntityBatchResult)
    {
        byte[] resultBytes = Convert.FromBase64String(encodedEntityBatchResult);
        P.EntityBatchResult result = P.EntityBatchResult.Parser.ParseFrom(resultBytes);
        result.IsWorkerDraining = true;
        return Convert.ToBase64String(result.ToByteArray());
    }
}
