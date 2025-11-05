// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Exceptions;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.DurableTask.Worker;
using Microsoft.DurableTask.Worker.Grpc;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

/// <summary>
/// A middleware to handle orchestration triggers.
/// </summary>
internal class DurableTaskFunctionsMiddleware(ExtendedSessionsCache extendedSessionsCache) : IFunctionsWorkerMiddleware
{
    private readonly ExtendedSessionsCache extendedSessionsCache = extendedSessionsCache;

    /// <inheritdoc />
    public Task Invoke(FunctionContext functionContext, FunctionExecutionDelegate next)
    {
        if (IsOrchestrationTrigger(functionContext, out BindingMetadata? triggerBinding))
        {
            return this.RunOrchestrationAsync(functionContext, triggerBinding, next);
        }

        if (IsEntityTrigger(functionContext, out triggerBinding))
        {
            return RunEntityAsync(functionContext, triggerBinding, next);
        }

        if (IsActivityTrigger(functionContext, out triggerBinding))
        {
            return RunActivityAsync(functionContext, triggerBinding, next);
        }

        return next(functionContext);
    }

    private static bool IsOrchestrationTrigger(
        FunctionContext context, [NotNullWhen(true)] out BindingMetadata? orchestrationTriggerBinding)
    {
        foreach (BindingMetadata binding in context.FunctionDefinition.InputBindings.Values)
        {
            if (string.Equals(binding.Type, "orchestrationTrigger", StringComparison.OrdinalIgnoreCase))
            {
                orchestrationTriggerBinding = binding;
                return true;
            }
        }

        orchestrationTriggerBinding = null;
        return false;
    }

    async Task RunOrchestrationAsync(
        FunctionContext context, BindingMetadata triggerBinding, FunctionExecutionDelegate next)
    {
        InputBindingData<object> triggerInputData = await context.BindInputAsync<object>(triggerBinding);
        if (triggerInputData?.Value is not string encodedOrchestratorState)
        {
            throw new InvalidOperationException("Orchestration history state was either missing from the input or not a string value.");
        }

        FunctionsOrchestrator orchestrator = new(context, next, triggerInputData);
        string orchestratorOutput = GrpcOrchestrationRunner.LoadAndRun(
            encodedOrchestratorState, orchestrator, this.extendedSessionsCache, context.InstanceServices);

        // Send the encoded orchestrator output as the return value seen by the functions host extension
        context.GetInvocationResult().Value = orchestratorOutput;
    }

    private static bool IsEntityTrigger(
        FunctionContext context, [NotNullWhen(true)] out BindingMetadata? entityTriggerBinding)
    {
        foreach (BindingMetadata binding in context.FunctionDefinition.InputBindings.Values)
        {
            if (string.Equals(binding.Type, "entityTrigger", StringComparison.OrdinalIgnoreCase))
            {
                entityTriggerBinding = binding;
                return true;
            }
        }

        entityTriggerBinding = null;
        return false;
    }

    static async Task RunEntityAsync(
        FunctionContext context, BindingMetadata triggerBinding, FunctionExecutionDelegate next)
    {
        InputBindingData<object> triggerInputData = await context.BindInputAsync<object>(triggerBinding);
        if (triggerInputData?.Value is not string encodedEntityBatch)
        {
            throw new InvalidOperationException("Entity batch was either missing from the input or not a string value.");
        }

        TaskEntityDispatcher dispatcher = new(encodedEntityBatch, context.InstanceServices);
        triggerInputData.Value = dispatcher;

        await next(context);
        context.GetInvocationResult().Value = dispatcher.Result;
    }

    private static bool IsActivityTrigger(
        FunctionContext context, [NotNullWhen(true)] out BindingMetadata? activityTriggerBinding)
    {
        foreach (BindingMetadata binding in context.FunctionDefinition.InputBindings.Values)
        {
            if (string.Equals(binding.Type, "activityTrigger", StringComparison.OrdinalIgnoreCase))
            {
                activityTriggerBinding = binding;
                return true;
            }
        }

        activityTriggerBinding = null;
        return false;
    }

    private static async Task RunActivityAsync(FunctionContext functionContext, BindingMetadata triggerBinding, FunctionExecutionDelegate next)
    {
        try
        {
            await next(functionContext);
            return;
        }
        catch (Exception ex)
        {
            // Get the exception properties provider from the service provider if available
            IExceptionPropertiesProvider? exceptionPropertiesProvider = functionContext.InstanceServices.GetService(typeof(IExceptionPropertiesProvider)) as IExceptionPropertiesProvider;
            throw new DurableSerializationException(ex, exceptionPropertiesProvider);
        }
    }
}
