// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Executors;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class OrchestratorInfo
    {
        internal OrchestratorInfo(ITriggeredFunctionExecutor executor, bool isOutOfProc)
        {
            this.Executor = executor;
            this.IsOutOfProc = isOutOfProc;
        }

        internal ITriggeredFunctionExecutor Executor { get; }

        internal bool IsOutOfProc { get; }
    }
}
