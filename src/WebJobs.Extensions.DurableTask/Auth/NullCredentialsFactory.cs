// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Auth;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Auth
{
    internal sealed class NullCredentialsFactory : IStorageCredentialsFactory
    {
        private NullCredentialsFactory()
        { }

        public static IStorageCredentialsFactory Instance { get; } = new NullCredentialsFactory();

        public Task<StorageCredentials> CreateAsync(AzureIdentityOptions options, CancellationToken cancellationToken = default) => null;

        public Task<StorageCredentials> CreateAsync(AzureIdentityOptions options, TimeSpan tokenRefreshOffset, TimeSpan tokenRefreshRetryDelay, CancellationToken cancellationToken = default) => null;
    }
}
