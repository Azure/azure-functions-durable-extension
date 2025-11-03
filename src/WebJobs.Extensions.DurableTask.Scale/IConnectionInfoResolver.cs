// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Azure.Core;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale
{
    /// <summary>
    /// Interface for resolving connection information.
    /// </summary>
    public interface IConnectionInfoResolver
    {
        /// <summary>
        /// Resolves connection information for a given connection name.
        /// </summary>
        /// <param name="connectionName">The name of the connection.</param>
        /// <returns>The connection string or token credential information.</returns>
        (string ConnectionString, TokenCredential Credential) ResolveConnectionInfo(string connectionName);
    }
}

