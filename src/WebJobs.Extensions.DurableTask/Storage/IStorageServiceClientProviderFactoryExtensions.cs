// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
using System;
using DurableTask.AzureStorage;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Storage
{
    internal static class IStorageServiceClientProviderFactoryExtensions
    {
        public static StorageAccountClientProvider GetClientProvider(this IStorageServiceClientProviderFactory clientProviderFactory, string connectionName)
        {
            if (clientProviderFactory == null)
            {
                throw new ArgumentNullException(nameof(clientProviderFactory));
            }

            return new StorageAccountClientProvider(
                clientProviderFactory.GetBlobClientProvider(connectionName),
                clientProviderFactory.GetQueueClientProvider(connectionName),
                clientProviderFactory.GetTableClientProvider(connectionName));
        }

        public static TrackingServiceClientProvider GetTrackingClientProvider(this IStorageServiceClientProviderFactory clientProviderFactory, string connectionName)
        {
            if (clientProviderFactory == null)
            {
                throw new ArgumentNullException(nameof(clientProviderFactory));
            }

            return new TrackingServiceClientProvider(
                clientProviderFactory.GetBlobClientProvider(connectionName),
                clientProviderFactory.GetTableClientProvider(connectionName));
        }
    }
}
