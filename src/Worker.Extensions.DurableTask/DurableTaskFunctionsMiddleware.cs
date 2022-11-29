// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.DurableTask.Worker.Grpc;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

internal class DurableTaskFunctionsMiddleware : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext functionContext, FunctionExecutionDelegate next)
    {
        if (!IsOrchestrationTrigger(functionContext, out BindingMetadata? triggerMetadata))
        {
            await next(functionContext);
            return;
        }

        InputBindingData<object> triggerInputData = await functionContext.BindInputAsync<object>(triggerMetadata);
        if (triggerInputData?.Value is not string encodedOrchestratorState)
        {
            throw new InvalidOperationException("Orchestration history state was either missing from the input or not a string value.");
        }

        // Wrap the function implementation in a lambda orchestrator
        string orchestratorOutput = GrpcOrchestrationRunner.LoadAndRun<object, object>(
            encodedOrchestratorState,
            async (orchestrationContext, input) =>
            {
                // Set the function input to be the orchestration context wrapped in our own object so that we can
                // intercept any of the calls and inject our own logic or tracking.
                FunctionsOrchestrationContext wrapperContext = new(orchestrationContext, functionContext);
                triggerInputData.Value = wrapperContext;

                // This method will advance to the next middleware and throw if it detects an asynchronous execution.
                await EnsureSynchronousExecution(functionContext, next, wrapperContext);

                // Set the raw function output as the orchestrator output
                object? functionOutput = functionContext.GetInvocationResult().Value;
                return functionOutput;
            },
            functionContext.InstanceServices);

        // Send the encoded orchestrator output as the return value seen by the functions host extension
        functionContext.GetInvocationResult().Value = orchestratorOutput;
    }

    private static bool IsOrchestrationTrigger(
        FunctionContext context,
        [NotNullWhen(true)] out BindingMetadata? orchestrationTriggerBinding)
    {
        foreach (BindingMetadata binding in context.FunctionDefinition.InputBindings.Values)
        {
            if (string.Equals(binding.Type, "orchestrationTrigger"))
            {
                orchestrationTriggerBinding = binding;
                return true;
            }
        }

        orchestrationTriggerBinding = null;
        return false;
    }

    private static async Task EnsureSynchronousExecution(
        FunctionContext functionContext,
        FunctionExecutionDelegate next,
        FunctionsOrchestrationContext orchestrationContext)
    {
        Task orchestratorTask = next(functionContext);
        if (!orchestratorTask.IsCompleted && !orchestrationContext.IsAccessed)
        {
            // If the middleware returns before the orchestrator function's context object was accessed and before
            // it completes its execution, then we know that either some middleware component went async or that the
            // orchestrator function did some illegal await as its very first action.
            throw new InvalidOperationException(Constants.IllegalAwaitErrorMessage);
        }

        await orchestratorTask;

        // This will throw if either the orchestrator performed an illegal await or if some middleware ahead of this
        // one performed some illegal await.
        orchestrationContext.ThrowIfIllegalAccess();
    }
}
