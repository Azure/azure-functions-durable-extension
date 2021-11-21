// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Interface defining methods to resolve connection strings.
    /// </summary>
    [Obsolete("Please use IConnectionInfoResolver instead.")]
    public interface IConnectionStringResolver
    {
        /// <summary>
        /// Looks up a connection string value given a name.
        /// </summary>
        /// <param name="connectionStringName">The name of the connection string.</param>
        /// <returns>Returns the resolved connection string value.</returns>
        string Resolve(string connectionStringName);
    }
}
