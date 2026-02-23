// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
using System;
using Microsoft.Azure.WebJobs.Host.Scale;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale
{
    /// <summary>
    /// Interface defining methods to build instances of <see cref="ScalabilityProvider"/>.
    /// </summary>
    public interface IScalabilityProviderFactory
    {
        /// <summary>
        /// Gets the provider factory name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the default connection name for this backend provider.
        /// </summary>
        string DefaultConnectionName { get; }

        /// <summary>
        /// Creates or retrieves a cached scalability provider.
        /// </summary>
        /// <param name="metadata">
        /// The Durable Task metadata containing task hub name, max concurrency settings, and storage provider configuration.
        /// </param>
        /// <param name="triggerMetadata">
        /// Trigger metadata provided by Scale Controller to access Properties like token credentials.
        /// </param>
        /// <returns>A scalability provider configured with the specified metadata.</returns>
        ScalabilityProvider GetScalabilityProvider(DurableTaskMetadata metadata, TriggerMetadata? triggerMetadata);
    }
}
