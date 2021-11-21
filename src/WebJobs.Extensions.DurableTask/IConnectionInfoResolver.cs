// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Interface defining methods to resolve connection information.
    /// </summary>
    internal interface IConnectionInfoResolver
    {
        /// <summary>
        /// Looks up the connection string given a name.
        /// </summary>
        /// <param name="name">The name of the connection string.</param>
        /// <returns>The resolved connection string or <see langword="null"/> if not found.</returns>
        string Resolve(string name);

        /// <summary>
        /// Looks up the connection info given a name.
        /// </summary>
        /// <typeparam name="T">The type of the information to resolve.</typeparam>
        /// <param name="name">The name of the connection information.</param>
        /// <returns>The resolved connection information or <see langword="null"/> if not found.</returns>
        T Resolve<T>(string name)
            where T : class;
    }
}
