// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using DurableTask.AzureStorage;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Auth;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal sealed class AzureStorageAccountProvider : IStorageAccountProvider
    {
        private readonly IConnectionInfoResolver connectionInfoResolver;
        private readonly IStorageCredentialsFactory credentialFactory;

        public AzureStorageAccountProvider(IConnectionInfoResolver connectionInfoResolver, IStorageCredentialsFactory credentialFactory = null)
        {
            this.connectionInfoResolver = connectionInfoResolver ?? throw new ArgumentNullException(nameof(credentialFactory));
            this.credentialFactory = credentialFactory ?? NullCredentialsFactory.Instance;
        }

        public StorageAccountDetails GetStorageAccountDetails(string name)
        {
            AzureStorageAccountOptions account = this.ResolveAccountInfo(name);
            if (!string.IsNullOrEmpty(account.ConnectionString))
            {
                return new StorageAccountDetails { ConnectionString = account.ConnectionString };
            }

            StorageCredentials storageCredentials = this.credentialFactory.CreateAsync(account).EnsureCompleted();
            if (storageCredentials == null)
            {
                throw new InvalidOperationException($"Unable to resolve the Azure Storage credentials for connection name '{name}'.");
            }

            if (account.QueueServiceUri != null || account.TableServiceUri != null)
            {
                // TODO: Use new endpoints when Durable Task is updated
                return new StorageAccountDetails
                {
                    StorageCredentials = storageCredentials,
                };
            }
            else
            {
                return new StorageAccountDetails
                {
                    AccountName = account.AccountName,
                    EndpointSuffix = AzureStorageAccountOptions.DefaultEndpointSuffix,
                    StorageCredentials = storageCredentials,
                };
            }
        }

        public CloudStorageAccount GetCloudStorageAccount(string name)
        {
            AzureStorageAccountOptions account = this.ResolveAccountInfo(name);
            if (!string.IsNullOrEmpty(account.ConnectionString))
            {
                return CloudStorageAccount.Parse(account.ConnectionString);
            }

            StorageCredentials storageCredentials = this.credentialFactory.CreateAsync(account).EnsureCompleted();
            if (storageCredentials == null)
            {
                throw new InvalidOperationException($"Unable to resolve the Azure Storage credentials for connection name '{name}'.");
            }

            if (account.QueueServiceUri != null || account.TableServiceUri != null)
            {
                return new CloudStorageAccount(
                    storageCredentials,
                    blobEndpoint: account.BlobServiceUri ?? account.GetDefaultServiceUri("blob"),
                    queueEndpoint: account.QueueServiceUri ?? account.GetDefaultServiceUri("queue"),
                    tableEndpoint: account.TableServiceUri ?? account.GetDefaultServiceUri("table"),
                    fileEndpoint: null);
            }
            else
            {
                return new CloudStorageAccount(
                    storageCredentials,
                    account.AccountName,
                    AzureStorageAccountOptions.DefaultEndpointSuffix,
                    useHttps: true);
            }
        }

        private AzureStorageAccountOptions ResolveAccountInfo(string name)
        {
            IConfigurationSection connection = this.connectionInfoResolver.Resolve(name);
            if (string.IsNullOrEmpty(connection.Value))
            {
#if FUNCTIONS_V1
                throw new InvalidOperationException($"Unable to resolve the Azure Storage connection named '{name}'.");
#else
                return connection.Get<AzureStorageAccountOptions>() ?? throw new InvalidOperationException($"Unable to resolve the Azure Storage connection named '{name}'.");
#endif
            }

            return new AzureStorageAccountOptions { ConnectionString = connection.Value };
        }
    }
}
