// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.ContextImplementations
{
    /// <summary>
    ///     Factory class to create Durable Client to start works outside an azure function context.
    /// </summary>
    public interface IDurableClientFactory
    {
        /// <summary>
        /// Gets a <see cref="IDurableClient"/> using configuration from a <see cref="DurableClientOptions"/> instance.
        /// </summary>
        /// <param name="durableClientOptions">options containing the client configuration parameters.</param>
        /// <returns>Returns a <see cref="IDurableClient"/> instance. The returned instance may be a cached instance.</returns>
        IDurableClient CreateClient(DurableClientOptions durableClientOptions);

        /// <summary>
        /// Gets a <see cref="IDurableClient"/> using configuration from a <see cref="DurableClientOptions"/> instance.
        /// </summary>
        /// <returns>Returns a <see cref="IDurableClient"/> instance. The returned instance may be a cached instance.</returns>
        IDurableClient CreateClient();
    }
}