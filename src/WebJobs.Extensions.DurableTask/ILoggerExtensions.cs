// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// ILogger extension methods.
    /// </summary>
    public static class ILoggerExtensions
    {
        /// <summary>
        /// Log Information only once when the IsReplaying is set to false.
        /// </summary>
        /// <param name="logger">Instance of <see cref="ILogger"/></param>
        /// <param name="contextBase">Instance of <see cref="DurableOrchestrationContextBase"/></param>
        /// <param name="message">Information message</param>
        public static void LogInformationOnce(this ILogger logger, DurableOrchestrationContextBase contextBase, string message)
        {
            if (!contextBase.IsReplaying)
            {
                logger.LogInformation(message);
            }
        }
    }
}
