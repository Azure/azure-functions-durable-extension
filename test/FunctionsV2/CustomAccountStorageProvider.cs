// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using DurableTask.AzureStorage;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.WindowsAzure.Storage;

namespace WebJobs.Extensions.DurableTask.Tests.V2
{
    internal class CustomAccountStorageProvider : IStorageAccountProvider
    {
        private readonly Dictionary<string, string> connectionStrings;

        public CustomAccountStorageProvider(Dictionary<string, string> connectionStrings)
        {
            this.connectionStrings = connectionStrings;
        }

        public CloudStorageAccount GetCloudStorageAccount(string name)
        {
            if (this.connectionStrings.TryGetValue(name, out string value))
            {
                return CloudStorageAccount.Parse(value);
            }

            return null;
        }

        public StorageAccountDetails GetStorageAccountDetails(string name)
        {
            if (this.connectionStrings.TryGetValue(name, out string value))
            {
                return new StorageAccountDetails { ConnectionString = value };
            }

            return null;
        }
    }
}
