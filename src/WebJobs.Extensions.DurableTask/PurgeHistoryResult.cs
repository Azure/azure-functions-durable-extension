// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Class to hold statistics about this execution of purge history.
    /// </summary>
    public class PurgeHistoryResult
    {
        /// <summary>
        /// Constructor for purge history statistics.
        /// </summary>
        /// <param name="instancesDeleted">Number of instances deleted.</param>
        public PurgeHistoryResult(int instancesDeleted)
        {
            this.InstancesDeleted = instancesDeleted;
        }

        /// <summary>
        /// Number of instances deleted during this execution of purge history.
        /// </summary>
        public int InstancesDeleted { get; }
    }
}
