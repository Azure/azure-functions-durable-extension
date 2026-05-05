// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using DurableTask.Core;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Exceptions;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Worker.Middleware;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution;

internal partial class DurableFunctionExecutor
{
    // Must point to a PUBLIC method.
    // Functions runtime will validate this, even though it is never called.
    public static readonly string ActivityEntryPoint =
        $"{typeof(DurableFunctionExecutor).FullName}.{nameof(Activity)}";

    public void Activity()
    {
        throw new NotImplementedException(
            "Do not call this method. It is a placeholder for activity function metadata.");
    }

    private async ValueTask RunActivityAsync(FunctionContext context, BindingMetadata triggerBinding)
    {
        try
        {
            if (context.FunctionDefinition.EntryPoint == ActivityEntryPoint)
            {
                await this.RunDirectActivityAsync(context, triggerBinding);
                return;
            }

            await inner.ExecuteAsync(context);
            return;
        }
        catch (Exception ex)
        {
            throw new DurableSerializationException(ex, exceptionPropertiesProvider);
        }
    }

    private async Task RunDirectActivityAsync(FunctionContext context, BindingMetadata triggerBinding)
    {
        if (!factory.TryCreateActivity(
            context.FunctionDefinition.Name, context.InstanceServices, out ITaskActivity? activity))
        {
            throw new InvalidOperationException(
                $"No activity with name '{context.FunctionDefinition.Name}' is registered.");
        }

        InputBindingData<object> triggerInputData = await context.BindInputAsync<object>(triggerBinding);
        if (triggerInputData?.Value is not string { } data)
        {
            throw new InvalidOperationException(
                "Activity input data was either missing from the input or not a JSON string.");
        }

        IMiddlewareFeatures features = CreateMiddlewareFeatures(context);
        TaskActivity shim = shimFactory.CreateActivity(
            context.FunctionDefinition.Name,
            activity,
            context.InstanceServices,
            features);
        TaskContext taskContext = new(new OrchestrationInstance
        {
            InstanceId = context.GetInstanceId(),
        });
        string? serializedOutput = await shim.RunAsync(taskContext, data);
        object? activityResult = serializedOutput is null
            ? null
            : this.Converter.Deserialize(serializedOutput, activity.OutputType);
        context.GetInvocationResult().Value = activityResult;
    }

}
