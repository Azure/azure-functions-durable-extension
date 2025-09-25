// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution;

internal class WrapperOrchestrator(ITaskOrchestrator inner, FunctionContext functionContext)
    : IDisposableOrchestrator
{
    public Type InputType => inner.InputType;

    public Type OutputType => inner.OutputType;

    public async Task<object?> RunAsync(TaskOrchestrationContext context, object? input)
    {
        FunctionsOrchestrationContext wrapperContext = new(context, functionContext);

        try
        {
            // This method will advance to the next middleware and throw if it detects an asynchronous execution.
            return await this.EnsureSynchronousExecutionAsync(wrapperContext, input);
        }
        catch (Exception ex)
        {
            functionContext.GetLogger<FunctionsOrchestrator>().LogError(
                ex,
                "An error occurred while executing the orchestrator function '{FunctionName}'.",
                functionContext.FunctionDefinition.Name);
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        if (inner is IAsyncDisposable asyncDisposable)
        {
            return asyncDisposable.DisposeAsync();
        }
        else if (inner is IDisposable disposable)
        {
            disposable.Dispose();
        }

        return default;
    }

    private async Task<object?> EnsureSynchronousExecutionAsync(
        FunctionsOrchestrationContext orchestrationContext, object? input)
    {
        Task<object?> orchestratorTask = inner.RunAsync(orchestrationContext, input);
        if (!orchestratorTask.IsCompleted && !orchestrationContext.IsAccessed)
        {
            // If the middleware returns before the orchestrator function's context object was accessed and before
            // it completes its execution, then we know that the orchestrator function did some illegal await as
            // its very first action.
            throw new InvalidOperationException(Constants.IllegalAwaitErrorMessage);
        }

        object? result = await orchestratorTask;

        // This will throw if either the orchestrator performed an illegal await.
        orchestrationContext.ThrowIfIllegalAccess();
        return result;
    }
}
