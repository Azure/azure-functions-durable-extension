// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
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
        /// Creates or retrieves a cached scalability provider.
        /// This method is used for both runtime-driven scaling and Scale Controller scenarios.
        /// </summary>
        /// <param name="metadata">
        /// The Durable Task metadata containing task hub name, max concurrency settings, and storage provider configuration.
        /// For runtime-driven scaling: constructed from DurableTaskOptions (host.json).
        /// For Scale Controller: deserialized from SyncTriggers payload.
        /// </param>
        /// <param name="triggerMetadata">
        /// Trigger metadata used to access Properties like token credentials (Scale Controller only).
        /// This is null for runtime-driven scaling since it runs in the host process.
        /// </param>
        /// <returns>A scalability provider configured with the specified metadata.</returns>
        ScalabilityProvider GetScalabilityProvider(DurableTaskMetadata metadata, TriggerMetadata? triggerMetadata);
    }
}
