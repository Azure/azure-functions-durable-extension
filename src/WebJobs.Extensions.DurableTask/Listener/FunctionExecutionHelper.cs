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
            IApplicationLifetimeWrapper hostServiceLifetime,
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
                    InvokeHandler = async userCodeHandler =>
                    {
                        executedUserCode = true;
                        await triggerInput.InvokeHandler(userCodeHandler);
                    },
#if !FUNCTIONS_V1
                    TriggerDetails = triggerInput.TriggerDetails,
#endif
                };
#pragma warning restore CS0618

                FunctionResult result = await executor.TryExecuteAsync(triggerInput, cancellationToken);
                if (!result.Succeeded)
                {
                    if (!executedUserCode &&
                         IsHostStopping(hostServiceLifetime))
                    {
                        return new WrappedFunctionResult(
                            WrappedFunctionResult.FunctionResultStatus.FunctionsRuntimeError,
                            result.Exception);
                    }
                    else
                    {
                        return new WrappedFunctionResult(
                            WrappedFunctionResult.FunctionResultStatus.UserCodeError,
                            result.Exception);
                    }
                }
                else
                {
                    return new WrappedFunctionResult(
                        WrappedFunctionResult.FunctionResultStatus.Success,
                        null);
                }
            }
            catch (Exception e)
            {
                return new WrappedFunctionResult(
                     WrappedFunctionResult.FunctionResultStatus.FunctionsRuntimeError,
                     e);
            }
        }

        public static async Task<WrappedFunctionResult> ExecuteActivityFunction(
            ITriggeredFunctionExecutor executor,
            IApplicationLifetimeWrapper hostServiceLifetime,
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

                if (!result.Succeeded)
                {
                    // Unfortunately, unlike with orchestrations and entities, we have no way
                    // to know if we hit user code or not. Therefore we need to look at the exception
                    // thrown by TryExecuteAsync to see if it is related to the host shutdown or not.
                    if (IsHostStopping(hostServiceLifetime) &&
                        IsHostShutdownRelatedException(result.Exception))
                    {
                        return new WrappedFunctionResult(
                             WrappedFunctionResult.FunctionResultStatus.FunctionsRuntimeError,
                             result.Exception);
                    }
                    else
                    {
                        return new WrappedFunctionResult(
                            WrappedFunctionResult.FunctionResultStatus.UserCodeError,
                            result.Exception);
                    }
                }
                else
                {
                    return new WrappedFunctionResult(WrappedFunctionResult.FunctionResultStatus.Success, null);
                }
            }
            catch (Exception e)
            {
                return new WrappedFunctionResult(
                     WrappedFunctionResult.FunctionResultStatus.FunctionsRuntimeError,
                     e);
            }
        }

        private static bool IsHostShutdownRelatedException(Exception ex)
        {
            // Currently, we don't have much knowledge of what exceptions are caused by host shutdown.
            // As we encounter more exceptions due to host shutdowns, we can add conditions here.
            return false;
        }

        private static bool IsHostStopping(IApplicationLifetimeWrapper hostServiceLifetime)
        {
            return hostServiceLifetime.OnStopped.IsCancellationRequested
                || hostServiceLifetime.OnStopping.IsCancellationRequested;
        }
    }
}
