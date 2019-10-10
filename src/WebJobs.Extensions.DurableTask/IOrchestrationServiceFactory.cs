// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DurableTask.Core;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Interface defining methods to build orchestration services and orchestration service clients.
    /// </summary>
    public interface IOrchestrationServiceFactory
    {
        /// <summary>
        /// Whether or not the orchestration services returned by this factory have support for Durable Entities.
        /// </summary>
        bool SupportsEntities { get; }

        /// <summary>
        /// Creates or retrieves a cached orchestration service to be used throughout the extension.
        /// </summary>
        /// <returns>An orchestration service to be used by the Durable Task Extension.</returns>
        IOrchestrationService GetOrchestrationService();

        /// <summary>
        /// Creates or retrieves a cached orchestration service client to be used in a given function execution.
        /// </summary>
        /// <param name="attribute">A durable client attribute with parameters for the orchestration client.</param>
        /// <returns>An orchestration service client to be used by a function.</returns>
        IOrchestrationServiceClient GetOrchestrationClient(DurableClientAttribute attribute);

        /// <summary>
        /// Storage providers must provide a way to get specialty client for operations not on
        /// <see cref="IOrchestrationService"/>.
        /// </summary>
        /// <param name="client">Task hub client used by specialty client.</param>
        /// <returns>Client to use special operations.</returns>
        IDurableSpecialOperationsClient GetSpecialtyClient(TaskHubClient client);

#if !NETSTANDARD2_0
        /// <summary>
        /// Gets a nonpopulated DurableTaskOptions for Functions V1 runtime to populate.
        /// </summary>
        /// <returns>An empty DurableTaskOptions of the appropriate type.</returns>
        DurableTaskOptions GetDefaultDurableTaskOptions();
#endif
    }
}
