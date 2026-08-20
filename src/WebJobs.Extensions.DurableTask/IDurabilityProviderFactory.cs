// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Sets the value of UseSeparateQueueForEntityWorkItems to be used for new DurabilityProviders created with this factory.
        /// </summary>
        /// <param name="newValue">The value of UseSeparateQueueForEntityWorkItems to use for all new providers.</param>
        /// <exception cref="NotImplementedException">Thrown if the implementation of this interface has not yet defined this method, caught and used for warnings in the extension.</exception>
        void SetUseSeparateQueueForEntityWorkItems(bool newValue)
        {
            throw new NotImplementedException($"The {this.Name} provider does not support SetUseSeparateQueueForEntityWorkItems.");
        }

        /// <summary>
        /// Passes the names of registered orchestrators, activities, and entities to the factory
        /// so that it can build work-item filters for backends that support selective dispatch (e.g., DTS).
        /// Called after function indexing completes but before the task hub worker starts.
        /// The default implementation is a no-op.
        /// </summary>
        /// <param name="orchestratorNames">The names of registered orchestrator functions.</param>
        /// <param name="activityNames">The names of registered activity functions.</param>
        /// <param name="entityNames">The names of registered entity functions.</param>
        void SetRegisteredFunctions(
            IReadOnlyCollection<string> orchestratorNames,
            IReadOnlyCollection<string> activityNames,
            IReadOnlyCollection<string> entityNames)
        {
            // No-op by default. Only backends that support work-item filtering need to override this.
        }
    }
}
