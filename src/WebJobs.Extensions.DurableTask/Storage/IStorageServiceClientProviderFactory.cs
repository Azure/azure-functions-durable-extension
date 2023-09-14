// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using DurableTask.AzureStorage;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Storage
{
    /// <summary>
    /// Defines methods for retrieving service client providers based on the connection name.
    /// </summary>
    internal interface IStorageServiceClientProviderFactory
    {
        /// <summary>
        /// Gets the <see cref="IStorageServiceClientProvider{BlobServiceClient, BlobClientOptions}"/> used
        /// for accessing the Azure Storage Blob Service associated with the <paramref name="connectionName"/>.
        /// </summary>
        /// <param name="connectionName">The name associated with the connection information.</param>
        /// <returns>The corresponding <see cref="IStorageServiceClientProvider{BlobServiceClient, BlobClientOptions}"/>.</returns>
        IStorageServiceClientProvider<BlobServiceClient, BlobClientOptions> GetBlobClientProvider(string connectionName);

        /// <summary>
        /// Gets the <see cref="IStorageServiceClientProvider{QueueServiceClient, QueueClientOptions}"/> used
        /// for accessing the Azure Storage Queue Service associated with the <paramref name="connectionName"/>.
        /// </summary>
        /// <param name="connectionName">The name associated with the connection information.</param>
        /// <returns>The corresponding <see cref="IStorageServiceClientProvider{QueueServiceClient, QueueClientOptions}"/>.</returns>
        IStorageServiceClientProvider<QueueServiceClient, QueueClientOptions> GetQueueClientProvider(string connectionName);

        /// <summary>
        /// Gets the <see cref="IStorageServiceClientProvider{TableServiceClient, TableClientOptions}"/> used
        /// for accessing the Azure Storage Table Service associated with the <paramref name="connectionName"/>.
        /// </summary>
        /// <param name="connectionName">The name associated with the connection information.</param>
        /// <returns>The corresponding <see cref="IStorageServiceClientProvider{TableServiceClient, TableClientOptions}"/>.</returns>
        IStorageServiceClientProvider<TableServiceClient, TableClientOptions> GetTableClientProvider(string connectionName);
    }
}
