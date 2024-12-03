// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using DurableTask.AzureStorage;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Storage;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal class CustomTestStorageAccountProvider : IStorageServiceClientProviderFactory
    {
        private readonly string customConnectionString;
        private readonly string customConnectionName;

        public CustomTestStorageAccountProvider(string connectionName)
        {
            this.customConnectionName = connectionName;
            this.customConnectionString = $"DefaultEndpointsProtocol=https;AccountName=test;AccountKey={GenerateRandomKey()};EndpointSuffix=core.windows.net";
        }

        public IStorageServiceClientProvider<BlobServiceClient, BlobClientOptions> GetBlobClientProvider(string connectionName) =>
            StorageServiceClientProvider.ForBlob(TestHelpers.GetStorageConnectionString());

        public IStorageServiceClientProvider<QueueServiceClient, QueueClientOptions> GetQueueClientProvider(string connectionName) =>
            StorageServiceClientProvider.ForQueue(TestHelpers.GetStorageConnectionString());

        public IStorageServiceClientProvider<TableServiceClient, TableClientOptions> GetTableClientProvider(string connectionName) =>
            StorageServiceClientProvider.ForTable(TestHelpers.GetStorageConnectionString());

        private static string GenerateRandomKey()
        {
            string key = Guid.NewGuid().ToString();
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(key));
        }
    }
}
