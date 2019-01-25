// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DurableTaskAzureStorage = DurableTask.AzureStorage;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Class to hold statistics about this execution of purge history.
    /// </summary>
    public class PurgeHistoryResult
    {
        private readonly DurableTaskAzureStorage.PurgeHistoryResult purgeHistoryResult;

        /// <summary>
        /// Constructor for purge history statistics.
        /// </summary>
        /// <param name="instancesDeleted">Number of instances deleted.</param>
        public PurgeHistoryResult(int instancesDeleted)
        {
            this.purgeHistoryResult = new DurableTaskAzureStorage.PurgeHistoryResult(0, instancesDeleted, 0);
        }

        /// <summary>
        /// Number of instances deleted during this execution of purge history.
        /// </summary>
        public int InstancesDeleted => this.purgeHistoryResult.InstancesDeleted;
    }
}
