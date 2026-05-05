// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Exceptions;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Worker.Middleware;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution;

internal partial class DurableFunctionExecutor
{
    static readonly MethodInfo BindFunctionActivityInputCoreAsyncMethod =
        typeof(DurableFunctionExecutor).GetMethod(
            nameof(BindFunctionActivityInputCoreAsync),
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Unable to find the activity input binding helper.");

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

            if (shimFactory.HasActivityMiddleware)
            {
                await this.RunFunctionActivityWithMiddlewareAsync(context, triggerBinding);
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
        InputBindingData<object> triggerInputData = await context.BindInputAsync<object>(triggerBinding);
        if (triggerInputData?.Value is not string { } data)
        {
            throw new InvalidOperationException(
                "Activity input data was either missing from the input or not a JSON string.");
        }

        context.GetInvocationResult().Value = await this.RunDirectActivityAsync(context, data);
    }

    internal async Task<object?> RunDirectActivityAsync(FunctionContext context, string data)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        if (!factory.TryCreateActivity(
            context.FunctionDefinition.Name, context.InstanceServices, out ITaskActivity? activity))
        {
            throw new InvalidOperationException(
                $"No activity with name '{context.FunctionDefinition.Name}' is registered.");
        }

        IMiddlewareFeatures features = CreateMiddlewareFeatures(context);
        TaskContext taskContext = new(new OrchestrationInstance
        {
            InstanceId = context.GetInstanceId(),
        });
        return await shimFactory.RunActivityAsync(
            context.FunctionDefinition.Name,
            activity,
            taskContext,
            data,
            context.InstanceServices,
            features);
    }

    static async Task<object?> BindFunctionActivityInputAsync(
        FunctionContext context,
        BindingMetadata triggerBinding,
        Type inputType)
    {
        MethodInfo method = BindFunctionActivityInputCoreAsyncMethod.MakeGenericMethod(inputType);
        object? task = method.Invoke(null, new object[] { context, triggerBinding });
        return await (Task<object?>)task!;
    }

    static async Task<object?> BindFunctionActivityInputCoreAsync<T>(
        FunctionContext context,
        BindingMetadata triggerBinding)
    {
        InputBindingData<T> data = await context.BindInputAsync<T>(triggerBinding);
        return data.Value;
    }

    static FunctionActivityInputDescriptor GetFunctionActivityInputDescriptor(
        FunctionContext context,
        BindingMetadata triggerBinding)
    {
        foreach (FunctionParameter parameter in context.FunctionDefinition.Parameters)
        {
            if (string.Equals(parameter.Name, triggerBinding.Name, StringComparison.OrdinalIgnoreCase)
                && parameter.Type != typeof(FunctionContext)
                && parameter.Type != typeof(CancellationToken))
            {
                return new(parameter.Type, true);
            }
        }

        return new(typeof(object), false);
    }

    async Task<FunctionActivityInput> BindFunctionActivityInputAsync(
        FunctionContext context,
        BindingMetadata triggerBinding)
    {
        FunctionActivityInputDescriptor descriptor = GetFunctionActivityInputDescriptor(context, triggerBinding);
        object? input = descriptor.HasInput
            ? await BindFunctionActivityInputAsync(context, triggerBinding, descriptor.InputType)
            : null;

        return CreateFunctionActivityInput(descriptor.InputType, descriptor.HasInput, input, this.Converter);
    }

    internal static FunctionActivityInput CreateFunctionActivityInput(
        Type inputType,
        bool hasInput,
        object? input,
        DataConverter converter)
    {
        if (inputType is null)
        {
            throw new ArgumentNullException(nameof(inputType));
        }

        if (converter is null)
        {
            throw new ArgumentNullException(nameof(converter));
        }

        string? rawInput = null;
        if (hasInput)
        {
            rawInput = input is null ? "null" : converter.Serialize(input);
        }

        return new(inputType, input, rawInput);
    }

    private async Task RunFunctionActivityWithMiddlewareAsync(
        FunctionContext context,
        BindingMetadata triggerBinding)
    {
        FunctionActivityInput input = await this.BindFunctionActivityInputAsync(context, triggerBinding);
        object? result = await this.RunFunctionActivityAsync(
            context,
            input,
            async () =>
            {
                await inner.ExecuteAsync(context);
                return context.GetInvocationResult().Value;
            });

        context.GetInvocationResult().Value = result;
    }

    internal Task<object?> RunFunctionActivityAsync(
        FunctionContext context,
        FunctionActivityInput input,
        Func<Task<object?>> body)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        if (body is null)
        {
            throw new ArgumentNullException(nameof(body));
        }

        IMiddlewareFeatures features = CreateMiddlewareFeatures(context);
        TaskContext taskContext = new(new OrchestrationInstance
        {
            InstanceId = context.GetInstanceId(),
        });
        ITaskActivity activity = new FunctionsActivity(input.InputType, body);
        return shimFactory.RunActivityAsync(
            context.FunctionDefinition.Name,
            activity,
            taskContext,
            input.RawInput,
            context.InstanceServices,
            features);
    }

    readonly record struct FunctionActivityInputDescriptor(Type InputType, bool HasInput);

    sealed class FunctionsActivity(Type inputType, Func<Task<object?>> body) : ITaskActivity
    {
        public Type InputType => inputType;

        public Type OutputType => typeof(object);

        public Task<object?> RunAsync(TaskActivityContext context, object? input)
        {
            return body();
        }
    }
}

internal sealed record FunctionActivityInput(Type InputType, object? Input, string? RawInput);
