// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
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
            TaskCommonShim shim,
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

                if (result.Succeeded)
                {
                    return WrappedFunctionResult.Success();
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return WrappedFunctionResult.FunctionHostStoppingFailure(result.Exception);
                }

                if (context.ExecutorCalledBack)
                {
                    // the problem did happen while the function code was executing.
                    // So it is either a user code failure or a function timeout.
                    if (result.Exception is Microsoft.Azure.WebJobs.Host.FunctionTimeoutException)
                    {
                        shim.TimeoutTriggered(result.Exception);
                        return WrappedFunctionResult.FunctionTimeoutFailure(result.Exception);
                    }
                    else
                    {
                        return WrappedFunctionResult.UserCodeFailure(result.Exception);
                    }
                }
                else
                {
                    // The problem happened before the function code even got called.
                    // This can happen if the constructor for a non-static function fails, for example.
                    // We want this to appear as if the function code threw the exception.
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
                if (cancellationToken.IsCancellationRequested)
                {
                    return WrappedFunctionResult.FunctionHostStoppingFailure(e);
                }
                else
                {
                    return WrappedFunctionResult.FunctionRuntimeFailure(e);
                }
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
                    return WrappedFunctionResult.FunctionHostStoppingFailure(result.Exception);
                }

                if (result.Exception is Microsoft.Azure.WebJobs.Host.FunctionTimeoutException)
                {
                    return WrappedFunctionResult.FunctionTimeoutFailure(result.Exception);
                }

                return WrappedFunctionResult.UserCodeFailure(result.Exception);
            }
            catch (Exception e)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return WrappedFunctionResult.FunctionHostStoppingFailure(e);
                }
                else
                {
                    return WrappedFunctionResult.FunctionRuntimeFailure(e);
                }
            }
        }
    }
}
