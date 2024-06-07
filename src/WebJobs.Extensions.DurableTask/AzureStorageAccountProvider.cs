// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using DurableTask.AzureStorage;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Extensions.Configuration;

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

        private readonly ConcurrentDictionary<string, TokenCredential> cachedTokenCredentials =
            new ConcurrentDictionary<string, TokenCredential>();

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

        /// <inheritdoc />
        public StorageAccountDetails GetStorageAccountDetails(string connectionName)
        {
            IConfigurationSection connectionInfo = this.connectionInfoResolver.Resolve(connectionName);
            if (!string.IsNullOrEmpty(connectionInfo.Value))
            {
                return new StorageAccountDetails { ConnectionString = connectionInfo.Value };
            }

#if !FUNCTIONS_V1
            AzureStorageAccountOptions account = connectionInfo.Get<AzureStorageAccountOptions>();
            if (account != null)
            {
                string key = !string.IsNullOrEmpty(account.AccountName) ? account.AccountName : account.BlobServiceUri.ToString();

                TokenCredential credential = this.cachedTokenCredentials.GetOrAdd(
                    key,
                    attr => this.credentialFactory.Create(connectionInfo));

                return new StorageAccountDetails
                {
                    AccountName = account.AccountName,
                    BlobServiceUri = account.BlobServiceUri,
                    EndpointSuffix = AzureStorageAccountOptions.DefaultEndpointSuffix,
                    QueueServiceUri = account.QueueServiceUri,
                    StorageCredentials = new StorageCredentials(credential),
                    TableServiceUri = account.TableServiceUri,
                };
            }
#endif
            throw new InvalidOperationException($"Unable to resolve the Azure Storage connection named '{connectionName}'.");
        }
    }
}
