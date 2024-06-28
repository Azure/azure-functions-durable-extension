// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
using System;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using DurableTask.AzureStorage;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Storage
{
    /// <inheritdoc cref="IStorageServiceClientProviderFactory"/>
    internal sealed class StorageServiceClientProviderFactory : IStorageServiceClientProviderFactory
    {
        private readonly IConnectionInfoResolver connectionInfoResolver;

        private readonly AzureComponentFactory componentFactory;
        private readonly AzureEventSourceLogForwarder logForwarder;

        public StorageServiceClientProviderFactory(IConnectionInfoResolver connectionInfoResolver, AzureComponentFactory componentFactory, AzureEventSourceLogForwarder logForwarder)
        {
            this.connectionInfoResolver = connectionInfoResolver ?? throw new ArgumentNullException(nameof(connectionInfoResolver));
            this.componentFactory = componentFactory ?? throw new ArgumentNullException(nameof(componentFactory));
            this.logForwarder = logForwarder ?? throw new ArgumentNullException(nameof(logForwarder));

            // Initialize the AzureEventSourceListener for all Azure Client SDKs, if it hasn't been started already
            this.logForwarder.Start();
        }

        /// <inheritdoc />
        public IStorageServiceClientProvider<BlobServiceClient, BlobClientOptions> GetBlobClientProvider(string connectionName)
        {
            // Check if a connection string has been specified
            IConfigurationSection connectionSection = this.connectionInfoResolver.Resolve(connectionName);
            if (!string.IsNullOrEmpty(connectionSection.Value))
            {
                return StorageServiceClientProvider.ForBlob(connectionSection.Value);
            }

            // Otherwise, check if any account information has been specified, like account name or service URI
            if (connectionSection.Exists())
            {
                return new BlobServiceClientProvider(connectionSection, this.componentFactory);
            }

            throw new InvalidOperationException($"Unable to resolve the Azure Storage connection named '{connectionName}'.");
        }

        /// <inheritdoc />
        public IStorageServiceClientProvider<QueueServiceClient, QueueClientOptions> GetQueueClientProvider(string connectionName)
        {
            // Check if a connection string has been specified
            IConfigurationSection connectionSection = this.connectionInfoResolver.Resolve(connectionName);
            if (!string.IsNullOrEmpty(connectionSection.Value))
            {
                return StorageServiceClientProvider.ForQueue(connectionSection.Value);
            }

            // Otherwise, check if any account information has been specified, like account name or service URI
            if (connectionSection.Exists())
            {
                return new QueueServiceClientProvider(connectionSection, this.componentFactory);
            }

            throw new InvalidOperationException($"Unable to resolve the Azure Storage connection named '{connectionName}'.");
        }

        /// <inheritdoc />
        public IStorageServiceClientProvider<TableServiceClient, TableClientOptions> GetTableClientProvider(string connectionName)
        {
            // Check if a connection string has been specified
            IConfigurationSection connectionSection = this.connectionInfoResolver.Resolve(connectionName);
            if (!string.IsNullOrEmpty(connectionSection.Value))
            {
                return StorageServiceClientProvider.ForTable(connectionSection.Value);
            }

            // Otherwise, check if any account information has been specified, like account name or service URI
            if (connectionSection.Exists())
            {
                return new TableServiceClientProvider(connectionSection, this.componentFactory);
            }

            throw new InvalidOperationException($"Unable to resolve the Azure Storage connection named '{connectionName}'.");
        }
    }
}
