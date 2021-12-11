// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Interface defining methods to resolve connection information.
    /// </summary>
    public interface IConnectionInfoResolver
    {
        /// <summary>
        /// Attempts to resolve the connection info given a name.
        /// </summary>
        /// <remarks>
        /// Depending on the configuration, the value may either be a connection string in the
        /// <see cref="IConfigurationSection.Value"/> property, or a user-defined type made
        /// up of one or more key-value pairs.
        /// </remarks>
        /// <param name="name">The name of the connection information.</param>
        /// <returns>The resolved connection information.</returns>
        IConfigurationSection Resolve(string name);
    }
}
