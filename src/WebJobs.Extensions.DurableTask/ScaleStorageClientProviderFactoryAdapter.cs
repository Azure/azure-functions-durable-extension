// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Azure.Core;
using DurableTask.AzureStorage;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Storage;

#nullable enable
namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Adapter that converts <see cref="Storage.IStorageServiceClientProviderFactory"/> to
    /// <see cref="Scale.IStorageServiceClientProviderFactory"/>.
    /// This allows the Azure Storage scalability provider to use host-loaded identities when runtime scaling is enabled,
    /// eliminating the need for the durabale-scale package to manage credentials directly.
    /// </summary>
    internal sealed class ScaleStorageClientProviderFactoryAdapter :
        Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.IStorageServiceClientProviderFactory
    {
        private readonly Microsoft.Azure.WebJobs.Extensions.DurableTask.Storage.IStorageServiceClientProviderFactory mainFactory;

        public ScaleStorageClientProviderFactoryAdapter(Microsoft.Azure.WebJobs.Extensions.DurableTask.Storage.IStorageServiceClientProviderFactory mainFactory)
        {
            this.mainFactory = mainFactory ?? throw new ArgumentNullException(nameof(mainFactory));
        }

        public StorageAccountClientProvider GetClientProvider(
            string connectionName,
            TokenCredential? tokenCredential = null)
        {
            // The main factory already handles identity via AzureComponentFactory,
            // so we delegate to it and ignore the tokenCredential parameter.
            return this.mainFactory.GetClientProvider(connectionName);
        }
    }
}
