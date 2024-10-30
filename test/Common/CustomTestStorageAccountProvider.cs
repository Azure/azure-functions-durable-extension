// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using DurableTask.AzureStorage;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal class CustomTestStorageAccountProvider : IStorageAccountProvider
    {
        private readonly string customConnectionString;
        private readonly string customConnectionName;

        public CustomTestStorageAccountProvider(string connectionName)
        {
            this.customConnectionName = connectionName;
            this.customConnectionString = $"DefaultEndpointsProtocol=https;AccountName=test;AccountKey={GenerateRandomKey()};EndpointSuffix=core.windows.net";
        }

        public CloudStorageAccount GetCloudStorageAccount(string name) =>
            CloudStorageAccount.Parse(name != this.customConnectionName ? TestHelpers.GetStorageConnectionString() : this.customConnectionString);

        public StorageAccountDetails GetStorageAccountDetails(string name) =>
            new StorageAccountDetails { ConnectionString = name != this.customConnectionName ? TestHelpers.GetStorageConnectionString() : this.customConnectionString };

        private static string GenerateRandomKey()
        {
            string key = Guid.NewGuid().ToString();
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(key));
        }
    }
}
