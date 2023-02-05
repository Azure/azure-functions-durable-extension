// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using DurableTask.AzureStorage;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Storage
{
    internal static class IStorageAccountExplorerExtensions
    {
        public static StorageAccountClientProvider GetClientProvider(this IAzureStorageAccountExplorer explorer, string connectionName)
        {
            if (explorer == null)
            {
                throw new ArgumentNullException(nameof(explorer));
            }

            return new StorageAccountClientProvider(
                explorer.GetBlobClientProvider(connectionName),
                explorer.GetQueueClientProvider(connectionName),
                explorer.GetTableClientProvider(connectionName));
        }
    }
}
