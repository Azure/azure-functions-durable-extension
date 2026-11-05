// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Options for restarting an orchestration with an explicit target instance ID.
    /// </summary>
    public sealed class RestartOrchestrationOptions
    {
        /// <summary>
        /// Gets or sets the required target instance ID.
        /// </summary>
        /// <value>A nonblank instance ID, which can equal the source instance ID.</value>
        public string NewInstanceId { get; set; }

        /// <summary>
        /// Gets or sets the optional version of the restarted orchestration.
        /// </summary>
        /// <value>
        /// The target version. A <c>null</c> value uses <see cref="DurableTaskOptions.DefaultVersion"/>;
        /// an empty string explicitly selects no version.
        /// </value>
        public string Version { get; set; }
    }
}
