// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Executors;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class RegisteredFunctionInfo
    {
        internal RegisteredFunctionInfo(ITriggeredFunctionExecutor executor, bool isOutOfProc)
        {
            this.Executor = executor;
            this.IsOutOfProc = isOutOfProc;
        }

        internal ITriggeredFunctionExecutor Executor { get; set; }

        // This flag is set when a function is disabled or the host is shutting down.
        internal bool IsDeregistered { get; set; }

        internal bool IsOutOfProc { get; }

        internal bool HasActiveListener => this.Executor != null && !this.IsDeregistered;
    }
}
