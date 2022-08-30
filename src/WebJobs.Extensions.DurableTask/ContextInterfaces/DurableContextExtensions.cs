// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Defines convenient overloads for calling the context methods, for all the contexts.
    /// </summary>
    public static class DurableContextExtensions
    {
        /// <summary>
        /// Returns an instance of ILogger that is replay safe, ensuring the logger logs only when the orchestrator
        /// is not replaying that line of code.
        /// </summary>
        /// <param name="context">The context object.</param>
        /// <param name="logger">An instance of ILogger.</param>
        /// <returns>An instance of a replay safe ILogger.</returns>
        public static ILogger CreateReplaySafeLogger(this IDurableOrchestrationContext context, ILogger logger)
        {
            return new ReplaySafeLogger(context, logger);
        }
    }
}
