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
        /// <param name="storageRequests">Requests sent to storage.</param>
        /// <param name="instancesDeleted">Number of instances deleted.</param>
        /// <param name="rowsDeleted">Number of rows deleted.</param>
        public PurgeHistoryResult(int storageRequests, int instancesDeleted, int rowsDeleted)
        {
            this.purgeHistoryResult = new DurableTaskAzureStorage.PurgeHistoryResult(
                storageRequests,
                instancesDeleted,
                rowsDeleted);
        }

        /// <summary>
        /// Number of requests sent to Storage during this execution of purge history.
        /// </summary>
        public int StorageRequests => this.purgeHistoryResult.StorageRequests;

        /// <summary>
        /// Number of instances deleted during this execution of purge history.
        /// </summary>
        public int InstancesDeleted => this.purgeHistoryResult.InstancesDeleted;

        /// <summary>
        /// Number of rows deleted during this execution of purge history.
        /// </summary>
        public int RowsDeleted => this.purgeHistoryResult.RowsDeleted;
    }
}
