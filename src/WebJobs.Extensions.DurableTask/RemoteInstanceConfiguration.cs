// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Configuration settings for <see cref="RemoteOrchestratorContext"> and <see cref="RemoteEntityContext">
    /// in out-of-process mode, transmitted via gRPC.
    /// </summary>
    internal class RemoteInstanceConfiguration
    {
        /// <summary>
        /// Gets or sets the default number of milliseconds between async HTTP status poll requests.
        /// </summary>
        internal int HttpDefaultAsyncRequestSleepTimeMilliseconds { get; set; } = 30000;

        /// <summary>
        /// Gets or sets whether or not to include the instance state in the instance request.
        /// True by default.
        /// </summary>
        internal bool IncludeState { get; set; } = true;

        /// <summary>
        /// Gets or sets whether or not the orchestration request is within an extended session.
        /// False by default.
        /// </summary>
        internal bool IsExtendedSession { get; set; } = false;

        /// <summary>
        /// Gets or sets the amount of time in seconds before an idle extended session times out.
        /// </summary>
        internal int ExtendedSessionIdleTimeoutInSeconds { get; set; }
    }
}
