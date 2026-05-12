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
    public async Task Invoke(FunctionContext functionContext, FunctionExecutionDelegate next)
    {
        // Set the function invocation ID for correlation with host-side logs.
        // This is used by the gRPC call invoker to add correlation headers.
        InvocationIdCallInvoker.SetCurrentInvocationId(functionContext.InvocationId);
        try
        {
            // If the function is a Durable Task function and there is no executor registered yet,
            // register the Durable Function executor.
            if (functionContext.Features.Get<IFunctionExecutor>() is null && functionContext.IsDurableTaskFunction())
            {
                functionContext.Features.Set<IFunctionExecutor>(invoker);
            }

            await next(functionContext);
        }
        finally
        {
            // Clear the invocation ID to prevent leaking to subsequent executions
            InvocationIdCallInvoker.SetCurrentInvocationId(null);
        }
    }
}
