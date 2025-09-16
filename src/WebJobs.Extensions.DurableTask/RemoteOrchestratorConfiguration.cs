// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Configuration settings for RemoteOrchestratorContext in out-of-process mode, transmitted via gRPC.
    /// </summary>
    public class RemoteOrchestratorConfiguration
    {
        /// <summary>
        /// Gets or sets the default number of milliseconds between async HTTP status poll requests.
        /// </summary>
        public int HttpDefaultAsyncRequestSleepTimeMilliseconds { get; set; } = 30000;

        /// <summary>
        /// Gets or sets whether or not to include the past history events in the orchestration request.
        /// True by default.
        /// </summary>
        public bool IncludePastEvents { get; set; } = true;

        /// <summary>
        /// Gets or sets whether or not the orchestration request is within an extended session.
        /// False by default.
        /// </summary>
        public bool IsExtendedSession { get; set; } = false;

        /// <summary>
        /// Gets or sets the amount of time in seconds before an idle extended session times out.
        /// </summary>
        public int ExtendedSessionIdleTimeoutInSeconds { get; set; }
    }
}
