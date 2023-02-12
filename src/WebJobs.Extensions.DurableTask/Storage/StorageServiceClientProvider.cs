// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Azure.Core;
using DurableTask.AzureStorage;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Storage
{
    internal abstract class StorageServiceClientProvider<TClient, TClientOptions, TConnectionOptions> : IStorageServiceClientProvider<TClient, TClientOptions>
        where TClientOptions : ClientOptions
        where TConnectionOptions : StorageServiceConnectionOptions
    {
        private readonly IConfigurationSection connectionSection;
        private readonly AzureComponentFactory componentFactory;

        protected StorageServiceClientProvider(IConfigurationSection connectionSection, AzureComponentFactory componentFactory, AzureEventSourceLogForwarder logForwarder)
        {
            this.connectionSection = connectionSection ?? throw new ArgumentNullException(nameof(connectionSection));
            this.componentFactory = componentFactory ?? throw new ArgumentNullException(nameof(componentFactory));
        }

        public TClient CreateClient(TClientOptions options)
        {
            // Azure Functions allows for additional connection options on top of what is provided by the AzureComponentFactory
            Uri serviceUri = this.connectionSection.Get<TConnectionOptions>()?.ServiceUri;
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
