// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DurableTask.AzureStorage;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal class TestStorageAccountProvider : IStorageAccountProvider
    {
        public CloudStorageAccount GetCloudStorageAccount(string name) =>
            CloudStorageAccount.Parse(TestHelpers.GetStorageConnectionString());

        public StorageAccountDetails GetStorageAccountDetails(string name) =>
            new StorageAccountDetails { ConnectionString = TestHelpers.GetStorageConnectionString() };
    }
}
