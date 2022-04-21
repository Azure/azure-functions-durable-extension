// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage.Auth;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Auth
{
    /// <summary>
    /// Defines methods for retrieving <see cref="TokenCredential"/>.
    /// </summary>
    internal interface ITokenCredentialFactory
    {
        /// <summary>
        /// Creates a <see cref="TokenCredential"/> based on the specified <paramref name="configuration"/>.
        /// </summary>
        /// <param name="configuration">A configuration containing information about the connection.</param>
        /// <param name="cancellationToken">
        /// An optional <see cref="CancellationToken"/> for prematurely canceling the operation.
        /// </param>
        /// <returns>
        /// The <see cref="TokenCredential"/> based on the specified <paramref name="configuration"/>.
        /// </returns>
        /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
        TokenCredential Create(IConfiguration configuration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a <see cref="TokenCredential"/> based on the specified <paramref name="configuration"/>.
        /// </summary>
        /// <param name="configuration">A configuration containing information about the connection.</param>
        /// <param name="tokenRefreshOffset">The offset from the token expiration date to begin refresh.</param>
        /// <param name="tokenRefreshRetryDelay">The amount of time to wait between refresh attempts in the event of a problem.</param>
        /// <param name="cancellationToken">
        /// An optional <see cref="CancellationToken"/> for prematurely canceling the operation.
        /// </param>
        /// <returns>
        /// The <see cref="TokenCredential"/> based on the specified <paramref name="configuration"/>.
        /// </returns>
        /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
        TokenCredential Create(
            IConfiguration configuration,
            TimeSpan tokenRefreshOffset,
            TimeSpan tokenRefreshRetryDelay,
            CancellationToken cancellationToken = default);
    }
}
