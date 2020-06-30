// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Listener
{
    internal static class FunctionExecutionHelper
    {
        public static async Task<WrappedFunctionResult> ExecuteFunctionInOrchestrationMiddleware(
            ITriggeredFunctionExecutor executor,
            TriggeredFunctionData triggerInput,
            DurableCommonContext context,
            CancellationToken cancellationToken)
        {
#pragma warning disable CS0618 // InvokeHandler approved for use by this extension
            if (triggerInput.InvokeHandler == null)
            {
                throw new ArgumentException(
                    $"{nameof(ExecuteFunctionInOrchestrationMiddleware)} should only be used when ${nameof(triggerInput)} has a value for ${nameof(TriggeredFunctionData.InvokeHandler)}");
            }

            try
            {
                context.ExecutorCalledBack = false;

                FunctionResult result = await executor.TryExecuteAsync(triggerInput, cancellationToken);

                if (context.ExecutorCalledBack)
                {
                    if (result.Succeeded)
                    {
                        return WrappedFunctionResult.Success();
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return WrappedFunctionResult.FunctionRuntimeFailure(result.Exception);
                    }

                    return WrappedFunctionResult.UserCodeFailure(result.Exception);
                }
                else
                {
                    // This can happen if the constructor for a non-static function fails.
                    // We want to treat this case exactly as if the function itself is throwing the exception.
                    // So we execute the middleware directly, instead of via the executor.
                    try
                    {
                        var exception = result.Exception ?? new InvalidOperationException("The function failed to start executing.");

                        await triggerInput.InvokeHandler(() => Task.FromException<object>(exception));

                        return WrappedFunctionResult.Success();
                    }
                    catch (Exception e) when (!cancellationToken.IsCancellationRequested)
                    {
                        return WrappedFunctionResult.UserCodeFailure(e);
                    }
                }
#pragma warning restore CS0618
            }
            catch (Exception e)
            {
                return WrappedFunctionResult.FunctionRuntimeFailure(e);
            }
        }

        public static async Task<WrappedFunctionResult> ExecuteActivityFunction(
            ITriggeredFunctionExecutor executor,
            TriggeredFunctionData triggerInput,
            CancellationToken cancellationToken)
        {
#pragma warning disable CS0618 // InvokeHandler approved for use by this extension
            if (triggerInput.InvokeHandler != null)
            {
                // Activity functions cannot use InvokeHandler, because the usage of InvokeHandler prevents the function from
                // returning a value in the way that the Activity shim knows how to handle.
                throw new ArgumentException(
                    $"{nameof(ExecuteActivityFunction)} cannot be used when ${nameof(triggerInput)} has a value for ${nameof(TriggeredFunctionData.InvokeHandler)}");
            }
#pragma warning restore CS0618

            try
            {
                FunctionResult result = await executor.TryExecuteAsync(triggerInput, cancellationToken);
                if (result.Succeeded)
                {
                    return WrappedFunctionResult.Success();
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return WrappedFunctionResult.FunctionRuntimeFailure(result.Exception);
                }

                return WrappedFunctionResult.UserCodeFailure(result.Exception);
            }
            catch (Exception e)
            {
                return WrappedFunctionResult.FunctionRuntimeFailure(e);
            }
        }
    }
}
