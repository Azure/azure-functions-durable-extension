// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

#nullable enable
namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Listener
{
    internal class WrappedFunctionResult
    {
        private WrappedFunctionResult(
            FunctionResultStatus status,
            Exception? ex)
        {
            this.Exception = ex;
            this.ExecutionStatus = status;
        }

        internal enum FunctionResultStatus
        {
            Success = 0, // the function executed successfully.
            UserCodeError = 1, // the user code had an unhandled exception
            FunctionsRuntimeError = 2, // the runtime could not execute the user code for some reason; considered transient and retried
            FunctionTimeoutError = 3, // execution timed out; treated as a user error
            FunctionsHostStoppingError = 4, // host was shutting down; treated as a functions runtime error
        }

        internal Exception? Exception { get; }

        internal FunctionResultStatus ExecutionStatus { get; }

        public static WrappedFunctionResult Success()
        {
            return new WrappedFunctionResult(FunctionResultStatus.Success, null);
        }

        public static WrappedFunctionResult FunctionRuntimeFailure(Exception ex)
        {
            return new WrappedFunctionResult(FunctionResultStatus.FunctionsRuntimeError, ex);
        }

        public static WrappedFunctionResult UserCodeFailure(Exception ex)
        {
            return new WrappedFunctionResult(FunctionResultStatus.UserCodeError, ex);
        }

        public static WrappedFunctionResult FunctionTimeoutFailure(Exception ex)
        {
            return new WrappedFunctionResult(FunctionResultStatus.FunctionTimeoutError, ex);
        }

        public static WrappedFunctionResult FunctionHostStoppingFailure(Exception ex)
        {
            return new WrappedFunctionResult(FunctionResultStatus.FunctionsHostStoppingError, ex);
        }
    }
}
