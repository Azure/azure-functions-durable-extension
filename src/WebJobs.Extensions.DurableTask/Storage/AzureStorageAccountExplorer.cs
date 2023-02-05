// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using DurableTask.AzureStorage;
#if !FUNCTIONS_V1
using Microsoft.Extensions.Azure;
#endif
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Storage
{
    internal sealed class AzureStorageAccountExplorer : IAzureStorageAccountExplorer
    {
        private readonly IConnectionInfoResolver connectionInfoResolver;

#if !FUNCTIONS_V1
        private readonly AzureComponentFactory componentFactory;
        private readonly AzureEventSourceLogForwarder logForwarder;

        public AzureStorageAccountExplorer(IConnectionInfoResolver connectionInfoResolver, AzureComponentFactory componentFactory, AzureEventSourceLogForwarder logForwarder)
        {
            this.connectionInfoResolver = connectionInfoResolver ?? throw new ArgumentNullException(nameof(connectionInfoResolver));
            this.componentFactory = componentFactory ?? throw new ArgumentNullException(nameof(componentFactory));
            this.logForwarder = logForwarder ?? throw new ArgumentNullException(nameof(logForwarder));
        }
#else
        public AzureStorageAccountExplorer(IConnectionInfoResolver connectionInfoResolver)
        {
            this.connectionInfoResolver = connectionInfoResolver ?? throw new ArgumentNullException(nameof(connectionInfoResolver));
        }
#endif

        /// <inheritdoc />
        public IStorageServiceClientProvider<BlobServiceClient, BlobClientOptions> GetBlobClientProvider(string connectionName)
        {
            IConfigurationSection connectionSection = this.connectionInfoResolver.Resolve(connectionName);
            if (!string.IsNullOrEmpty(connectionSection.Value))
            {
                return StorageServiceClientProvider.ForBlob(connectionSection.Value);
            }

#if !FUNCTIONS_V1
            if (connectionSection.Exists())
            {
                return new BlobServiceClientProvider(connectionSection, this.componentFactory, this.logForwarder);
            }
#endif
            throw new InvalidOperationException($"Unable to resolve the Azure Storage connection named '{connectionName}'.");
        }

        /// <inheritdoc />
        public IStorageServiceClientProvider<QueueServiceClient, QueueClientOptions> GetQueueClientProvider(string connectionName)
        {
            IConfigurationSection connectionSection = this.connectionInfoResolver.Resolve(connectionName);
            if (!string.IsNullOrEmpty(connectionSection.Value))
            {
                return StorageServiceClientProvider.ForQueue(connectionSection.Value);
            }

#if !FUNCTIONS_V1
            if (connectionSection.Exists())
            {
                return new QueueServiceClientProvider(connectionSection, this.componentFactory, this.logForwarder);
            }
#endif
            throw new InvalidOperationException($"Unable to resolve the Azure Storage connection named '{connectionName}'.");
        }

        /// <inheritdoc />
        public IStorageServiceClientProvider<TableServiceClient, TableClientOptions> GetTableClientProvider(string connectionName)
        {
            IConfigurationSection connectionSection = this.connectionInfoResolver.Resolve(connectionName);
            if (!string.IsNullOrEmpty(connectionSection.Value))
            {
                return StorageServiceClientProvider.ForTable(connectionSection.Value);
            }

#if !FUNCTIONS_V1
            if (connectionSection.Exists())
            {
                return new TableServiceClientProvider(connectionSection, this.componentFactory, this.logForwarder);
            }
#endif
            throw new InvalidOperationException($"Unable to resolve the Azure Storage connection named '{connectionName}'.");
        }
    }
}
