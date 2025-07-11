// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Configuration settings for RemoteEntityContext in out-of-process mode, transmitted via gRPC.
    /// </summary>
    public class RemoteEntityConfiguration
    {
        /// <summary>
        /// Gets or sets whether or not to include the entity state in the entity batch request.
        /// </summary>
        public bool IncludeEntityState { get; set; } = true;

        /// <summary>
        /// Gets or sets whether or not the entity batch request is within an extended session.
        /// </summary>
        public bool ExtendedSession { get; set; } = false;

        /// <summary>
        /// Gets or sets the amount of time in seconds before an idle extended session times out.
        /// </summary>
        public int ExtendedSessionIdleTimeoutInSeconds { get; set; }
    }
}
