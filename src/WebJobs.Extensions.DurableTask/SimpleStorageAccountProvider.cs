// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using DurableTask.AzureStorage;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class SimpleStorageAccountProvider : IStorageAccountProvider
    {
        private readonly IConnectionStringResolver connectionStringResolver;

        public SimpleStorageAccountProvider(IConnectionStringResolver connectionStringResolver)
        {
            this.connectionStringResolver = connectionStringResolver ?? throw new ArgumentNullException(nameof(connectionStringResolver));
        }

        public virtual StorageAccountDetails GetStorageAccountDetails(string name)
        {
            if (!this.TryResolveConnectionString(name, out string connectionString))
            {
                throw new InvalidOperationException($"Unable to resolve the Azure Storage connection named '{name}'.");
            }

            return new StorageAccountDetails { ConnectionString = connectionString };
        }

        public virtual CloudStorageAccount GetCloudStorageAccount(string name)
        {
            if (!this.TryResolveConnectionString(name, out string connectionString))
            {
                throw new InvalidOperationException($"Unable to resolve the Azure Storage connection named '{name}'.");
            }

            return CloudStorageAccount.Parse(connectionString);
        }

        protected bool TryResolveConnectionString(string name, out string connectionString)
        {
            connectionString = name != null ? this.connectionStringResolver.Resolve(name) : null;
            return !string.IsNullOrEmpty(connectionString);
        }
    }
}
