// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using Microsoft.Azure.WebJobs.Host.Scale;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Interface defining methods to build instances of <see cref="DurabilityProvider"/>.
    /// </summary>
    public interface IDurabilityProviderFactory
    {
        /// <summary>
        /// Specifies the Durability Provider Factory name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Creates or retrieves a durability provider to be used throughout the extension.
        /// </summary>
        /// <returns>An durability provider to be used by the Durable Task Extension.</returns>
        DurabilityProvider GetDurabilityProvider();

        /// <summary>
        /// Creates or retrieves a cached durability provider to be used in a given function execution.
        /// </summary>
        /// <param name="attribute">A durable client attribute with parameters for the durability provider.</param>
        /// <returns>A durability provider to be used by a client function.</returns>
        DurabilityProvider GetDurabilityProvider(DurableClientAttribute attribute);

        /// <summary>
        /// Creates or retrieves a cached durability provider to be used in a given function execution.
        /// </summary>
        /// <param name="attribute">A durable client attribute with parameters for the durability provider.</param>
        /// <param name="triggerMetadata">Trigger Metadata which contains </param>
        /// <returns>A durability provider to be used by a client function.</returns>
        DurabilityProvider GetDurabilityProvider(DurableClientAttribute attribute, TriggerMetadata triggerMetadata);
    }
}
