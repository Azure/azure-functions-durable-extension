// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DurableTask.AzureStorage;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Defines methods for retrieving Azure Storage account metadata.
    /// </summary>
    internal interface IStorageAccountProvider
    {
        /// <summary>
        /// Gets the <see cref="StorageAccountDetails"/> associated with the <paramref name="connectionName"/>.
        /// </summary>
        /// <param name="connectionName">The name associated with the connection information.</param>
        /// <returns>The corresponding <see cref="StorageAccountDetails"/>.</returns>
        StorageAccountDetails GetStorageAccountDetails(string connectionName);

        /// <summary>
        /// Gets the <see cref="CloudStorageAccount"/> associated with the <paramref name="connectionName"/>.
        /// </summary>
        /// <param name="connectionName">The name associated with the connection information.</param>
        /// <returns>The corresponding <see cref="CloudStorageAccount"/>.</returns>
        CloudStorageAccount GetCloudStorageAccount(string connectionName);
    }
}
