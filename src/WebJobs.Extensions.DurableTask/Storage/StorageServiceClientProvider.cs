// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
using System;
using Azure.Core;
using DurableTask.AzureStorage;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Storage
{
    /// <inheritdoc cref="IStorageServiceClientProvider{TClient, TClientOptions}"/>
    internal abstract class StorageServiceClientProvider<TClient, TClientOptions, TConnectionOptions> : IStorageServiceClientProvider<TClient, TClientOptions>
        where TClientOptions : ClientOptions
        where TConnectionOptions : StorageServiceConnectionOptions
    {
        private readonly IConfigurationSection connectionSection;
        private readonly AzureComponentFactory componentFactory;

        protected StorageServiceClientProvider(IConfigurationSection connectionSection, AzureComponentFactory componentFactory)
        {
            // When configuring their Durable Functions extension, users optionally specify the key, or "connection name,"
            // for their Azure Storage connection metadata within the configuration, which may be either be a
            // connection string or a collection of properties like Account Name, Service URI, etc.
            // That section is "connectionSection."
            this.connectionSection = connectionSection ?? throw new ArgumentNullException(nameof(connectionSection));
            this.componentFactory = componentFactory ?? throw new ArgumentNullException(nameof(componentFactory));
        }

        public TClient CreateClient(TClientOptions options)
        {
            // The CreateClient method creates a BlobServiceClient, QueueServiceClient, or TableServiceClient based on
            // user-specified values witin the "connectionSection." This section may either contain a Service URI
            // or a Connection String, which dictates how the client is constructed. It also may contain credential
            // metadata, like whether to use managed identities.
            //
            // The separation of CreateClient and CreateOptions was done to allow for the DTFx to insert its own
            // policies into the options before the client creation, like for logging retries, throttling, etc.
            //
            // Note:
            // We do not wholly defer to the AzureComponentFactory, as Azure Functions allows for the use of Blob, Queue,
            // and Table client information in the same configuration section. AzureComponentFactory uses the same
            // name "ServiceUri" for all of the clients by default because it is the same name used by their respective
            // ctor parameters. However, Azure Functions requires a way to disambiguate these URIs in the same section.
            // Therefore, it uses the names "BlobServiceUri," "QueueServiceUri," and "TableServiceUri."
            Uri? serviceUri = this.connectionSection.Get<TConnectionOptions>()?.ServiceUri;
            TokenCredential tokenCredential = this.componentFactory.CreateTokenCredential(this.connectionSection);

            return serviceUri != null
                ? this.CreateClient(serviceUri, tokenCredential, options)
                : (TClient)this.componentFactory.CreateClient(typeof(TClient), this.connectionSection, tokenCredential, options);
        }

        public TClientOptions CreateOptions()
        {
            return (TClientOptions)this.componentFactory.CreateClientOptions(typeof(TClientOptions), null, this.connectionSection);
        }

        protected abstract TClient CreateClient(Uri serviceUri, TokenCredential tokenCredential, TClientOptions options);
    }
}
