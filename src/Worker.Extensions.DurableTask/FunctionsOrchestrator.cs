// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

/// <summary>
/// A custom orchestrator for running functions-triggered orchestrations.
/// </summary>
internal class FunctionsOrchestrator : ITaskOrchestrator
{
    private readonly FunctionContext functionContext;
    private readonly FunctionExecutionDelegate next;
    private readonly InputBindingData<object> contextBinding;
    private readonly OrchestrationInputConverter.InputContext inputContext;

    public FunctionsOrchestrator(
        FunctionContext functionContext,
        FunctionExecutionDelegate next,
        InputBindingData<object> contextBinding)
    {
        this.functionContext = functionContext;
        this.next = next;
        this.contextBinding = contextBinding;
        this.inputContext = OrchestrationInputConverter.GetInputContext(functionContext);
    }

    /// <inheritdoc />
    public Type InputType => this.inputContext.Type;

    /// <inheritdoc />
    public Type OutputType => typeof(object);

    /// <inheritdoc />
    public async Task<object?> RunAsync(TaskOrchestrationContext context, object? input)
    {
        // Set the function input to be the orchestration context wrapped in our own object so that we can
        // intercept any of the calls and inject our own logic or tracking.
        FunctionsOrchestrationContext wrapperContext = new(context, this.functionContext);
        this.contextBinding.Value = wrapperContext;
        this.inputContext.PrepareInput(input);

        try
        {
            // This method will advance to the next middleware and throw if it detects an asynchronous execution.
            await EnsureSynchronousExecution(this.functionContext, this.next, wrapperContext);
        }
        catch (Exception ex)
        {
            this.functionContext.GetLogger<FunctionsOrchestrator>().LogError(
                ex,
                "An error occurred while executing the orchestrator function '{FunctionName}'.",
                this.functionContext.FunctionDefinition.Name);
            throw;
        }

        // Set the raw function output as the orchestrator output
        object? functionOutput = this.functionContext.GetInvocationResult().Value;
        return functionOutput;
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
