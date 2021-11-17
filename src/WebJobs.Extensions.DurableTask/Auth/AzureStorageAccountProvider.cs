// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using DurableTask.AzureStorage;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Auth
{
    internal sealed class AzureStorageAccountProvider : SimpleStorageAccountProvider
    {
        private readonly IStorageCredentialsFactory credentialFactory;
        private readonly IConfiguration configuration;

        public AzureStorageAccountProvider(
            IConnectionStringResolver connectionStringResolver,
            IStorageCredentialsFactory credentialFactory,
            IConfiguration configuration)
            : base(connectionStringResolver)
        {
            this.credentialFactory = credentialFactory ?? throw new ArgumentNullException(nameof(credentialFactory));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public override StorageAccountDetails GetStorageAccountDetails(string name)
        {
            if (this.TryResolveConnectionString(name, out string connectionString))
            {
                return new StorageAccountDetails { ConnectionString = connectionString };
            }

            AzureStorageAccountOptions options = this.GetAzureStorageAccountOptions(this.configuration, name);
            StorageCredentials storageCredentials = this.credentialFactory.CreateAsync(options).EnsureCompleted();

            if (options.QueueServiceUri != null || options.TableServiceUri != null)
            {
                if (options.QueueServiceUri == null || options.TableServiceUri == null)
                {
                    throw new InvalidOperationException(
                        $"Both {nameof(AzureStorageAccountOptions.QueueServiceUri)} and {nameof(AzureStorageAccountOptions.TableServiceUri)} must be specified.");
                }

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
                    AccountName = options.AccountName,
                    EndpointSuffix = AzureStorageAccountOptions.DefaultEndpointSuffix,
                    StorageCredentials = storageCredentials,
                };
            }
        }

        public override CloudStorageAccount GetCloudStorageAccount(string name)
        {
            if (this.TryResolveConnectionString(name, out string connectionString))
            {
                return CloudStorageAccount.Parse(connectionString);
            }

            AzureStorageAccountOptions options = this.GetAzureStorageAccountOptions(this.configuration, name);
            StorageCredentials storageCredentials = this.credentialFactory.CreateAsync(options).EnsureCompleted();

            if (options.QueueServiceUri != null || options.TableServiceUri != null)
            {
                if (options.QueueServiceUri == null || options.TableServiceUri == null)
                {
                    throw new InvalidOperationException(
                        $"Both {nameof(AzureStorageAccountOptions.QueueServiceUri)} and {nameof(AzureStorageAccountOptions.TableServiceUri)} must be specified.");
                }

                return new CloudStorageAccount(
                    storageCredentials,
                    blobEndpoint: null,
                    queueEndpoint: options.QueueServiceUri,
                    tableEndpoint: options.TableServiceUri,
                    fileEndpoint: null);
            }
            else
            {
                return new CloudStorageAccount(
                    storageCredentials,
                    options.AccountName,
                    AzureStorageAccountOptions.DefaultEndpointSuffix,
                    useHttps: true);
            }
        }

        private AzureStorageAccountOptions GetAzureStorageAccountOptions(IConfiguration configuration, string name)
        {
            AzureStorageAccountOptions options = configuration.GetSection(name).Get<AzureStorageAccountOptions>();
            if (options == null)
            {
                throw new InvalidOperationException($"Unable to resolve the Azure Storage connection named '{name}'.");
            }
            else if (!options.UseManagedIdentity)
            {
                throw new InvalidOperationException("Only 'managedidentity' credentials are supported.");
            }

            return options;
        }
    }
}
