// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution;
using Microsoft.Azure.Functions.Worker.Invocation;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

/// <summary>
/// A middleware to handle orchestration triggers.
/// </summary>
internal class DurableTaskFunctionsMiddleware(DurableFunctionExecutor invoker) : IFunctionsWorkerMiddleware
{
    /// <inheritdoc />
    public Task Invoke(FunctionContext functionContext, FunctionExecutionDelegate next)
    {
        if (functionContext.TryGetOrchestrationBinding(out _)
            || functionContext.TryGetEntityBinding(out _)
            || functionContext.TryGetActivityBinding(out _))
        {
            functionContext.Features.Set<IFunctionExecutor>(invoker);
        }

        return next(functionContext);
    }
}
