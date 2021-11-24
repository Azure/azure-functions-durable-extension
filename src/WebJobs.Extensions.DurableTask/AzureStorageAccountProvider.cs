// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using DurableTask.AzureStorage;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
#if !FUNCTIONS_V1
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Auth;
using Microsoft.WindowsAzure.Storage.Auth;
#endif

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal sealed class AzureStorageAccountProvider : IStorageAccountProvider
    {
        private readonly IConnectionInfoResolver connectionInfoResolver;

#if !FUNCTIONS_V1
        private readonly ITokenCredentialFactory credentialFactory;

        public AzureStorageAccountProvider(IConnectionInfoResolver connectionInfoResolver, ITokenCredentialFactory credentialFactory)
        {
            this.connectionInfoResolver = connectionInfoResolver ?? throw new ArgumentNullException(nameof(connectionInfoResolver));
            this.credentialFactory = credentialFactory ?? throw new ArgumentNullException(nameof(credentialFactory));
        }
#else
        public AzureStorageAccountProvider(IConnectionInfoResolver connectionInfoResolver)
        {
            this.connectionInfoResolver = connectionInfoResolver ?? throw new ArgumentNullException(nameof(connectionInfoResolver));
        }
#endif

        public StorageAccountDetails GetStorageAccountDetails(string name)
        {
            IConfigurationSection connectionInfo = this.connectionInfoResolver.Resolve(name);
            if (!string.IsNullOrEmpty(connectionInfo.Value))
            {
                return new StorageAccountDetails { ConnectionString = connectionInfo.Value };
            }

#if FUNCTIONS_V1
            throw new InvalidOperationException($"Unable to resolve the Azure Storage connection named '{name}'.");
#else
            AzureStorageAccountOptions account = connectionInfo.Get<AzureStorageAccountOptions>() ?? throw new InvalidOperationException($"Unable to resolve the Azure Storage connection named '{name}'.");
            TokenCredential credential = this.credentialFactory.Create(connectionInfo);

            if (account.QueueServiceUri != null || account.TableServiceUri != null)
            {
                // TODO: Use new endpoints when Durable Task is updated
                return new StorageAccountDetails
                {
                    StorageCredentials = new StorageCredentials(credential),
                };
            }
            else
            {
                return new StorageAccountDetails
                {
                    AccountName = account.AccountName,
                    EndpointSuffix = AzureStorageAccountOptions.DefaultEndpointSuffix,
                    StorageCredentials = new StorageCredentials(credential),
                };
            }
#endif
        }

        public CloudStorageAccount GetCloudStorageAccount(string name)
        {
            IConfigurationSection connectionInfo = this.connectionInfoResolver.Resolve(name);
            if (!string.IsNullOrEmpty(connectionInfo.Value))
            {
                return CloudStorageAccount.Parse(connectionInfo.Value);
            }

#if FUNCTIONS_V1
            throw new InvalidOperationException($"Unable to resolve the Azure Storage connection named '{name}'.");
#else
            AzureStorageAccountOptions account = connectionInfo.Get<AzureStorageAccountOptions>()
                ?? throw new InvalidOperationException($"Unable to resolve the Azure Storage connection named '{name}'.");
            TokenCredential credential = this.credentialFactory.Create(connectionInfo);

            if (account.QueueServiceUri != null || account.TableServiceUri != null)
            {
                return new CloudStorageAccount(
                    new StorageCredentials(credential),
                    blobEndpoint: account.BlobServiceUri ?? account.GetDefaultServiceUri("blob"),
                    queueEndpoint: account.QueueServiceUri ?? account.GetDefaultServiceUri("queue"),
                    tableEndpoint: account.TableServiceUri ?? account.GetDefaultServiceUri("table"),
                    fileEndpoint: null);
            }
            else
            {
                return new CloudStorageAccount(
                    new StorageCredentials(credential),
                    account.AccountName,
                    AzureStorageAccountOptions.DefaultEndpointSuffix,
                    useHttps: true);
            }
#endif
        }
    }
}
