// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Listener
{
    internal class WrappedFunctionResult
    {
        private WrappedFunctionResult(
            FunctionResultStatus status,
            Exception ex)
        {
            this.Exception = ex;
            this.ExecutionStatus = status;
        }

        internal enum FunctionResultStatus
        {
            Success = 0,
            UserCodeError = 1,
            FunctionsRuntimeError = 2,
        }

        internal Exception Exception { get; }

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
    }
}
