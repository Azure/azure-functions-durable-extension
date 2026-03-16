// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
using Azure.Core;
using DurableTask.AzureStorage;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale
{
    /// <summary>
    /// Defines methods for retrieving Azure Storage backend service client providers based on the connection name.
    /// </summary>
    public interface IStorageServiceClientProviderFactory
    {
        /// <summary>
        /// Gets the <see cref="StorageAccountClientProvider"/> used
        /// for accessing the Azure Storage services associated with the <paramref name="connectionName"/>.
        /// </summary>
        /// <param name="connectionName">The name associated with the connection information.</param>
        /// <param name="tokenCredential">Optional token credential for Managed Identity scenarios.</param>
        /// <returns>The corresponding <see cref="StorageAccountClientProvider"/>.</returns>
        StorageAccountClientProvider GetClientProvider(string connectionName, TokenCredential? tokenCredential = null);
    }
}
