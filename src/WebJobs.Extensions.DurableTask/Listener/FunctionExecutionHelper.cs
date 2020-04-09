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
                bool executedUserCode = false;
                var triggeredFunctionData = new TriggeredFunctionData()
                {
                    TriggerValue = triggerInput.TriggerValue,
                    ParentId = triggerInput.ParentId,
                    InvokeHandler = userCodeHandler =>
                    {
                        executedUserCode = true;
                        return triggerInput.InvokeHandler(userCodeHandler);
                    },
#if !FUNCTIONS_V1
                    TriggerDetails = triggerInput.TriggerDetails,
#endif
                };
#pragma warning restore CS0618

                FunctionResult result = await executor.TryExecuteAsync(triggerInput, cancellationToken);

                if (result.Succeeded)
                {
                    return WrappedFunctionResult.Success();
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return WrappedFunctionResult.FunctionRuntimeFailure(result.Exception);
                }

                if (!executedUserCode)
                {
                    WrappedFunctionResult.UserCodeFailure(new InvalidOperationException(
                         "The function failed to start executing. " +
                         "For .NET functions, this can happen if an unhandled exception is thrown in the function's class constructor."));
                }

                return WrappedFunctionResult.UserCodeFailure(result.Exception);
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
