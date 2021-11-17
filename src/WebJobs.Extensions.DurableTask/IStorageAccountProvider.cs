// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DurableTask.AzureStorage;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Defines methods for retrieving Azure Storage account metadata.
    /// </summary>
    public interface IStorageAccountProvider
    {
        /// <summary>
        /// Gets the <see cref="StorageAccountDetails"/> associated with the <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The name containing account information.</param>
        /// <remarks>
        /// Depending on the implementation, this may either be a connection string or a configuration section.
        /// </remarks>
        /// <returns>The corresponding <see cref="StorageAccountDetails"/>.</returns>
        StorageAccountDetails GetStorageAccountDetails(string name);

        /// <summary>
        /// Gets the <see cref="CloudStorageAccount"/> associated with the <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The name containing account information.</param>
        /// <remarks>
        /// Depending on the implementation, this may either be a connection string or a configuration section.
        /// </remarks>
        /// <returns>The corresponding <see cref="CloudStorageAccount"/>.</returns>
        CloudStorageAccount GetCloudStorageAccount(string name);
    }
}
