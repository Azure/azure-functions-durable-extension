// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.DurableTask.Worker.Grpc;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

/// <summary>
/// A middleware to handle orchestration triggers.
/// </summary>
internal class DurableTaskFunctionsMiddleware : IFunctionsWorkerMiddleware
{
    /// <inheritdoc />
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

        FunctionsOrchestrator orchestrator = new(functionContext, next, triggerInputData);
        string orchestratorOutput = GrpcOrchestrationRunner.LoadAndRun(
            encodedOrchestratorState, orchestrator, functionContext.InstanceServices);

        // Send the encoded orchestrator output as the return value seen by the functions host extension
        functionContext.GetInvocationResult().Value = orchestratorOutput;
    }

    private static bool IsOrchestrationTrigger(
        FunctionContext context, [NotNullWhen(true)] out BindingMetadata? orchestrationTriggerBinding)
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
}
