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
        /// Creates or retrieves a cached orchestration service to be used throughout the extension.
        /// </summary>
        /// <returns>An orchestration service to be used by the Durable Task Extension.</returns>
        IOrchestrationService GetOrchestrationService();

        /// <summary>
        /// Creates or retrieves a cached orchestration service client to be used in a given function execution.
        /// </summary>
        /// <param name="attribute">An orchestration client attribute with parameters for the orchestration client.</param>
        /// <returns>An orchestration service client to be used by a function.</returns>
        IOrchestrationServiceClient GetOrchestrationClient(OrchestrationClientAttribute attribute);
    }
}
