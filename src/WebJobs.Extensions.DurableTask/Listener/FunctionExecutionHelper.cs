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

        public static async Task<WrappedFunctionResult> ExecuteFunction(
            ITriggeredFunctionExecutor executor,
            IApplicationLifetimeWrapper hostServiceLifetime,
            TriggeredFunctionData triggerInput,
            CancellationToken cancellationToken)
        {
#pragma warning disable CS0618 // InvokeHandler approved for use by this extension
            //if (triggerInput.InvokeHandler != null)
            //{
            //    // Activity functions cannot use InvokeHandler, because the usage of InvokeHandler prevents the function from
            //    // returning a value in the way that the Activity shim knows how to handle.
            //    throw new ArgumentException(
            //        $"{nameof(ExecuteFunction)} cannot be used when ${nameof(triggerInput)} has a value for ${nameof(TriggeredFunctionData.InvokeHandler)}");
            //}
#pragma warning restore CS0618

            try
            {
                FunctionResult result = await executor.TryExecuteAsync(triggerInput, cancellationToken);

                if (!result.Succeeded)
                {
                    // This is a best effort approach to determine if this is caused by an exception in
                    // the webjobs pipeline. False positives are alright, as we still only
                    // will abort the session when the host is performing a shutdown,
                    // so it will just fail normally on the second execution.
                    if (IsHostStopping(hostServiceLifetime) &&
                        IsHostRelatedException(result.Exception))
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

        private static bool IsHostRelatedException(Exception ex)
        {
            // We look at any exception thrown in the Microsoft.Azure.WebJobs namespace that is not
            // an exception thrown by us, as we know there are many reasons that we could
            // throw an exception that are unrelated to host shutdown.
            return ex.Source.StartsWith("Microsoft.Azure.WebJobs") &&
                !ex.Source.StartsWith("Microsoft.Azure.WebJobs.Extensions.DurableTask");
        }

        private static bool IsHostStopping(IApplicationLifetimeWrapper hostServiceLifetime)
        {
            return hostServiceLifetime.OnStopped.IsCancellationRequested
                || hostServiceLifetime.OnStopping.IsCancellationRequested;
        }
    }
}
