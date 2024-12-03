// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
using System;
using Azure.Core;
using Azure.Data.Tables;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Storage
{
    internal sealed class TableServiceClientProvider : StorageServiceClientProvider<TableServiceClient, TableClientOptions, TableServiceClientProvider.ConnectionOptions>
    {
        public TableServiceClientProvider(IConfigurationSection connectionSection, AzureComponentFactory componentFactory)
            : base(connectionSection, componentFactory)
        {
        }

        protected override TableServiceClient CreateClient(Uri serviceUri, TokenCredential tokenCredential, TableClientOptions options)
        {
            return new TableServiceClient(serviceUri, tokenCredential, options);
        }

        internal sealed class ConnectionOptions : StorageServiceConnectionOptions
        {
            public Uri? TableServiceUri { get; set; }

            public override Uri? ServiceUri => this.TableServiceUri ?? base.ServiceUri;

            protected override string ServiceName => "table";
        }
    }
}
