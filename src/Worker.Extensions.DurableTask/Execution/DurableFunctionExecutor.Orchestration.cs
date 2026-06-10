// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Google.Protobuf;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Worker.Grpc;
using P = Microsoft.DurableTask.Protobuf;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution;

internal partial class DurableFunctionExecutor
{
    // Must point to a PUBLIC method.
    // Functions runtime will validate this, even though it is never called.
    public static readonly string OrchestrationEntryPoint =
        $"{typeof(DurableFunctionExecutor).FullName}.{nameof(Orchestration)}";

    public void Orchestration()
    {
        throw new NotImplementedException(
            "Do not call this method. It is a placeholder for orchestration function metadata.");
    }

    private async ValueTask RunOrchestrationAsync(FunctionContext context, BindingMetadata triggerBinding)
    {
        InputBindingData<object> triggerInputData = await context.BindInputAsync<object>(triggerBinding);
        if (triggerInputData?.Value is not string encodedOrchestratorState)
        {
            throw new InvalidOperationException(
                "Orchestration history state was either missing from the input or not a string value.");
        }

        await using IDisposableOrchestrator orchestrator = this.CreateOrchestrator(context, triggerInputData);
        string orchestratorOutput = GrpcOrchestrationRunner.LoadAndRun(
            encodedOrchestratorState, orchestrator, extendedSessionsCache, context.InstanceServices);

        if (this.IsWorkerDraining)
        {
            orchestratorOutput = FlagOrchestratorDraining(orchestratorOutput);
        }

        // Send the encoded orchestrator output as the return value seen by the functions host extension
        context.GetInvocationResult().Value = orchestratorOutput;
    }

    private static string FlagOrchestratorDraining(string encodedOrchestratorOutput)
    {
        byte[] responseBytes = Convert.FromBase64String(encodedOrchestratorOutput);
        P.OrchestratorResponse response = P.OrchestratorResponse.Parser.ParseFrom(responseBytes);
        response.IsWorkerDraining = true;
        return Convert.ToBase64String(response.ToByteArray());
    }

    private IDisposableOrchestrator CreateOrchestrator(
        FunctionContext context, InputBindingData<object> triggerInputData)
    {
        if (context.FunctionDefinition.EntryPoint == OrchestrationEntryPoint)
        {
            return this.CreateWrapper(context);
        }

        return new FunctionsOrchestrator(context, inner, triggerInputData);
    }

    private WrapperOrchestrator CreateWrapper(FunctionContext context)
    {
        if (!factory.TryCreateOrchestrator(
            context.FunctionDefinition.Name, context.InstanceServices, out ITaskOrchestrator? orchestrator))
        {
            throw new InvalidOperationException(
                $"No orchestrator with name '{context.FunctionDefinition.Name}' is registered.");
        }

        return new(orchestrator, context);
    }
}
