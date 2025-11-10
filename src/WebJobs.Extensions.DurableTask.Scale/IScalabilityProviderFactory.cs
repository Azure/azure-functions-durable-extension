// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using System;
using Microsoft.Azure.WebJobs.Host.Scale;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale
{
    /// <summary>
    /// Interface defining methods to build instances of <see cref="ScalabilityProvider"/>.
    /// </summary>
    public interface IScalabilityProviderFactory
    {
        /// <summary>
        /// Specifies the Durability Provider Factory name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the default connection name for this backend provider.
        /// </summary>
        string DefaultConnectionName { get; }

        /// <summary>
        /// Creates or retrieves a scalability provider to be used throughout the extension.
        /// </summary>
        /// <returns>A scalability provider to be used by the Durable Task Extension.</returns>
        ScalabilityProvider GetScalabilityProvider();

        /// <summary>
        /// Creates or retrieves a cached scalability provider to be used in a given function execution.
        /// This overload accepts pre-deserialized metadata to avoid double deserialization of the metadata payload.
        /// The triggerMetadata is still passed to allow access to Properties (e.g., token credentials).
        /// </summary>
        /// <param name="metadata">The pre-deserialized Durable Task metadata from the trigger.</param>
        /// <param name="triggerMetadata">Trigger metadata used to access Properties like token credentials.</param>
        /// <returns>A scalability provider to be used by a client function.</returns>
        ScalabilityProvider GetScalabilityProvider(DurableTaskMetadata metadata, TriggerMetadata triggerMetadata);
    }
}
