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
        // If the function is a Durable Task function and there is no executor registered yet,
        // register the Durable Function executor.
        if (functionContext.Features.Get<IFunctionExecutor>() is null && functionContext.IsDurableTaskFunction())
        {
            functionContext.Features.Set<IFunctionExecutor>(invoker);
        }

        return next(functionContext);
    }
}
