// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using DurableTask.AzureStorage;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Storage;

namespace WebJobs.Extensions.DurableTask.Tests.V2
{
    internal class CustomStorageServiceClientProviderFactory : IStorageServiceClientProviderFactory
    {
        private readonly Dictionary<string, string> connectionStrings;

        public CustomStorageServiceClientProviderFactory(Dictionary<string, string> connectionStrings)
        {
            this.connectionStrings = connectionStrings;
        }

        public IStorageServiceClientProvider<BlobServiceClient, BlobClientOptions> GetBlobClientProvider(string connectionName) =>
            this.connectionStrings.TryGetValue(connectionName, out string value) ? StorageServiceClientProvider.ForBlob(value) : null;

        public IStorageServiceClientProvider<QueueServiceClient, QueueClientOptions> GetQueueClientProvider(string connectionName) =>
            this.connectionStrings.TryGetValue(connectionName, out string value) ? StorageServiceClientProvider.ForQueue(value) : null;

        public IStorageServiceClientProvider<TableServiceClient, TableClientOptions> GetTableClientProvider(string connectionName) =>
            this.connectionStrings.TryGetValue(connectionName, out string value) ? StorageServiceClientProvider.ForTable(value) : null;
    }
}
