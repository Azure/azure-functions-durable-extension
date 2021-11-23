// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Auth;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Auth
{
    /// <summary>
    /// Defines methods for retrieving <see cref="StorageCredentials"/>.
    /// </summary>
    internal interface IStorageCredentialsFactory
    {
        /// <summary>
        /// Asynchronously creates <see cref="StorageCredentials"/> based on the specified <paramref name="options"/>.
        /// </summary>
        /// <param name="options">Information about the user to authenticate.</param>
        /// <param name="cancellationToken">
        /// An optional <see cref="CancellationToken"/> for prematurely canceling the asynchronous operation.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation. The value of the <see cref="Task{TResult}.Result"/>
        /// property contains the <see cref="StorageCredentials"/> based on the specified <paramref name="options"/>.
        /// </returns>
        /// <exception cref="OperationCanceledException">The resulting task was canceled.</exception>
        Task<StorageCredentials> CreateAsync(AzureIdentityOptions options, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously creates <see cref="StorageCredentials"/> based on the specified <paramref name="options"/>.
        /// </summary>
        /// <param name="options">Information about the user to authenticate.</param>
        /// <param name="tokenRefreshOffset">The offset from the token expiration date to begin refresh.</param>
        /// <param name="tokenRefreshRetryDelay">The amount of time to wait between refresh attempts in the event of a problem.</param>
        /// <param name="cancellationToken">
        /// An optional <see cref="CancellationToken"/> for prematurely canceling the asynchronous operation.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation. The value of the <see cref="Task{TResult}.Result"/>
        /// property contains the <see cref="StorageCredentials"/> based on the specified <paramref name="options"/>.
        /// </returns>
        /// <exception cref="OperationCanceledException">The resulting task was canceled.</exception>
        Task<StorageCredentials> CreateAsync(
            AzureIdentityOptions options,
            TimeSpan tokenRefreshOffset,
            TimeSpan tokenRefreshRetryDelay,
            CancellationToken cancellationToken = default);
    }
}
