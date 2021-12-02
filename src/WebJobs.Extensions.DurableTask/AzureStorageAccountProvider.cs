// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Globalization;
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
            throw new InvalidOperationException($"Unable to resolve the Azure Storage connection string named '{name}'.");
#else
            AzureStorageAccountOptions account = connectionInfo.Get<AzureStorageAccountOptions>()
                ?? throw new InvalidOperationException($"Unable to resolve the Azure Storage connection string named '{name}'.");
            TokenCredential credential = this.credentialFactory.Create(connectionInfo);

            if (account.BlobServiceUri != null || account.QueueServiceUri != null || account.TableServiceUri != null)
            {
                ValidateServiceUris(account);
                return new StorageAccountDetails
                {
                    BlobServiceUri = account.BlobServiceUri,
                    QueueServiceUri = account.QueueServiceUri,
                    StorageCredentials = new StorageCredentials(credential),
                    TableServiceUri = account.TableServiceUri,
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
            throw new InvalidOperationException($"Unable to resolve the Azure Storage connection string named '{name}'.");
#else
            AzureStorageAccountOptions account = connectionInfo.Get<AzureStorageAccountOptions>()
                ?? throw new InvalidOperationException($"Unable to resolve the Azure Storage connection string named '{name}'.");
            TokenCredential credential = this.credentialFactory.Create(connectionInfo);

            if (account.BlobServiceUri != null || account.QueueServiceUri != null || account.TableServiceUri != null)
            {
                ValidateServiceUris(account);
                return new CloudStorageAccount(
                    new StorageCredentials(credential),
                    blobEndpoint: account.BlobServiceUri,
                    queueEndpoint: account.QueueServiceUri,
                    tableEndpoint: account.TableServiceUri,
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

        private static void ValidateServiceUris(AzureStorageAccountOptions account)
        {
            if (account.BlobServiceUri == null || account.QueueServiceUri == null || account.TableServiceUri == null)
            {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "If at least one Azure Storage service URI is specified, {0}, {1}, and {1} must all be provided.",
                        nameof(AzureStorageAccountOptions.BlobServiceUri),
                        nameof(AzureStorageAccountOptions.QueueServiceUri),
                        nameof(AzureStorageAccountOptions.TableServiceUri)));
            }
        }
    }
}
