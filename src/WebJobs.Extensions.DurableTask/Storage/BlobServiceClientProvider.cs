// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
using System;
using Azure.Core;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Storage
{
    internal sealed class BlobServiceClientProvider : StorageServiceClientProvider<BlobServiceClient, BlobClientOptions, BlobServiceClientProvider.ConnectionOptions>
    {
        public BlobServiceClientProvider(IConfigurationSection connectionSection, AzureComponentFactory componentFactory)
            : base(connectionSection, componentFactory)
        {
        }

        protected override BlobServiceClient CreateClient(Uri serviceUri, TokenCredential tokenCredential, BlobClientOptions options)
        {
            return new BlobServiceClient(serviceUri, tokenCredential, options);
        }

        internal sealed class ConnectionOptions : StorageServiceConnectionOptions
        {
            public Uri? BlobServiceUri { get; set; }

            public override Uri? ServiceUri => this.BlobServiceUri ?? base.ServiceUri;

            protected override string ServiceName => "blob";
        }
    }
}
