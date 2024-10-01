// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DurableTask.AzureStorage;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal class CustomTestStorageAccountProvider : IStorageAccountProvider
    {
        private const string CustomConnectionString = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=dGVzdA==;EndpointSuffix=core.windows.net";
        private readonly string customConnectionName;

        public CustomTestStorageAccountProvider(string connectionName)
        {
            this.customConnectionName = connectionName;
        }

        public CloudStorageAccount GetCloudStorageAccount(string name) =>
            CloudStorageAccount.Parse(name != this.customConnectionName ? TestHelpers.GetStorageConnectionString() : CustomConnectionString);

        public StorageAccountDetails GetStorageAccountDetails(string name) =>
            new StorageAccountDetails { ConnectionString = name != this.customConnectionName ? TestHelpers.GetStorageConnectionString() : CustomConnectionString };
    }
}
