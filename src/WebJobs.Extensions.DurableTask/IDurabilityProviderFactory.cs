// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using System;
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
        /// <param name="triggerMetadata">Trigger metadata used to create IOrchestrationService for functions scale scenarios.</param>
        /// <returns>A durability provider to be used by a client function.</returns>
        DurabilityProvider GetDurabilityProvider(DurableClientAttribute attribute, TriggerMetadata triggerMetadata)
        {
            // This method is not supported by this provider.
            // Only providers that require TriggerMetadata for scale should implement it.
            throw new NotImplementedException("This provider does not support GetDurabilityProvider with TriggerMetadata.");
        }
    }
}
