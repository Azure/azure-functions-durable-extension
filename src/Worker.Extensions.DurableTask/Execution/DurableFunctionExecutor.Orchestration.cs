// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.DurableTask.Worker.Grpc;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution;

internal partial class DurableFunctionExecutor
{
    private async ValueTask RunOrchestrationAsync(FunctionContext context, BindingMetadata triggerBinding)
    {
        InputBindingData<object> triggerInputData = await context.BindInputAsync<object>(triggerBinding);
        if (triggerInputData?.Value is not string encodedOrchestratorState)
        {
            throw new InvalidOperationException(
                "Orchestration history state was either missing from the input or not a string value.");
        }

        FunctionsOrchestrator orchestrator = new(context, inner, triggerInputData);
        string orchestratorOutput = GrpcOrchestrationRunner.LoadAndRun(
            encodedOrchestratorState, orchestrator, extendedSessionsCache, context.InstanceServices);

        // Send the encoded orchestrator output as the return value seen by the functions host extension
        context.GetInvocationResult().Value = orchestratorOutput;
    }
}
