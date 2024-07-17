// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
using System;
using Azure.Core;
using Azure.Storage.Queues;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Storage
{
    internal sealed class QueueServiceClientProvider : StorageServiceClientProvider<QueueServiceClient, QueueClientOptions, QueueServiceClientProvider.ConnectionOptions>
    {
        public QueueServiceClientProvider(IConfigurationSection connectionSection, AzureComponentFactory componentFactory)
            : base(connectionSection, componentFactory)
        {
        }

        protected override QueueServiceClient CreateClient(Uri serviceUri, TokenCredential tokenCredential, QueueClientOptions options)
        {
            return new QueueServiceClient(serviceUri, tokenCredential, options);
        }

        internal sealed class ConnectionOptions : StorageServiceConnectionOptions
        {
            public Uri? QueueServiceUri { get; set; }

            public override Uri? ServiceUri => this.QueueServiceUri ?? base.ServiceUri;

            protected override string ServiceName => "queue";
        }
    }
}
