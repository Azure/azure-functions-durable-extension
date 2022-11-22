// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Represents the out-of-proc protocols supported by this extension.
    /// </summary>
    internal enum OutOfProcOrchestrationProtocol
    {
        /// <summary>
        /// Out-of-proc works by replaying SDK actions in an orchestration "shim", which interprets
        /// the output of an out-of-proc language SDK and makes DTFx API calls on behalf of the
        /// out-of-process orchestrator.
        /// </summary>
        /// <remarks>
        /// This protocol has been deprecated. All new language SDK should instead use
        /// <see cref="MiddlewarePassthrough"/>.
        /// </remarks>
        OrchestratorShim = 0,

        /// <summary>
        /// Out-of-proc works by forwarding all outputs directly to the Durable Task Framework
        /// without using any orchestrator "shims" or attempting any interpretation. This is
        /// sometimes referred to as "out of proc v2".
        /// </summary>
        MiddlewarePassthrough = 1,
    }
}
