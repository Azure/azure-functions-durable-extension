// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale
{
    /// <summary>
    /// Interface for resolving names of application settings.
    /// </summary>
    public interface INameResolver
    {
        /// <summary>
        /// Resolves an application setting name to its value.
        /// </summary>
        /// <param name="name">The name of the application setting.</param>
        /// <returns>The resolved value, or the original name if no resolution is found.</returns>
        string Resolve(string name);
    }
}

