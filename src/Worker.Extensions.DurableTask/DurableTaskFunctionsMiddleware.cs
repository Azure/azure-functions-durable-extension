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
        if (!IsOrchestrationTrigger(functionContext, out BindingMetadata? triggerMetadata, out Type? inputType))
        {
            await next(functionContext);
            return;
        }

        InputBindingData<object> triggerInputData = await functionContext.BindInputAsync<object>(triggerMetadata);
        if (triggerInputData?.Value is not string encodedOrchestratorState)
        {
            throw new InvalidOperationException("Orchestration history state was either missing from the input or not a string value.");
        }

        FunctionsOrchestrator orchestrator = new(functionContext, next, triggerInputData, inputType);
        string orchestratorOutput = GrpcOrchestrationRunner.LoadAndRun(
            encodedOrchestratorState, orchestrator, functionContext.InstanceServices);

        // Send the encoded orchestrator output as the return value seen by the functions host extension
        functionContext.GetInvocationResult().Value = orchestratorOutput;
    }

    private static bool IsOrchestrationTrigger(
        FunctionContext context,
        [NotNullWhen(true)] out BindingMetadata? orchestrationTriggerBinding,
        out Type? inputType)
    {
        static Type? GetInputType(FunctionDefinition definition)
        {
            foreach (FunctionParameter parameter in definition.Parameters)
            {
                if (!definition.InputBindings.ContainsKey(parameter.Name)
                    && !definition.OutputBindings.ContainsKey(parameter.Name))
                {
                    // We assume the first parameter with exactly 0 bindings is the input.
                    return parameter.Type;
                }
            }

            return null;
        }

        FunctionDefinition definition = context.FunctionDefinition;
        foreach (BindingMetadata binding in definition.InputBindings.Values)
        {
            if (string.Equals(binding.Type, "orchestrationTrigger"))
            {
                orchestrationTriggerBinding = binding;
                inputType = GetInputType(definition);
                return true;
            }
        }

        orchestrationTriggerBinding = null;
        inputType = null;
        return false;
    }
}
