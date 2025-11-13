// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Exceptions;
using Microsoft.DurableTask;

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

        object? input = this.Converter.Deserialize(data, activity.InputType);
        object? activityResult = await activity.RunAsync(new FunctionsTaskActivityContext(context), input);
        context.GetInvocationResult().Value = activityResult;
    }

    private sealed class FunctionsTaskActivityContext(FunctionContext context)
        : TaskActivityContext
    {
        public FunctionContext Context { get; } = context;

        public override TaskName Name { get; } = context.FunctionDefinition.Name;

        public override string InstanceId { get; } = context.GetInstanceId();
    }
}
