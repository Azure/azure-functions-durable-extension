// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Provides functionality available to durable entity clients.
    /// </summary>
    public interface IDurableEntityClient
    {
        /// <summary>
        /// Gets the name of the task hub configured on this client instance.
        /// </summary>
        /// <value>
        /// The name of the task hub.
        /// </value>
        string TaskHubName { get; }

        /// <summary>
        /// Signals an entity to perform an operation.
        /// </summary>
        /// <param name="entityId">The target entity.</param>
        /// <param name="operationName">The name of the operation.</param>
        /// <param name="operationInput">The input for the operation.</param>
        /// <param name="taskHubName">The TaskHubName of the target entity.</param>
        /// <param name="connectionName">The name of the connection string associated with <paramref name="taskHubName"/>.</param>
        /// <returns>A task that completes when the message has been reliably enqueued.</returns>
        Task SignalEntityAsync(EntityId entityId, string operationName, object operationInput = null, string taskHubName = null, string connectionName = null);

        /// <summary>
        /// Signals an entity to perform an operation, at a specified time.
        /// </summary>
        /// <param name="entityId">The target entity.</param>
        /// <param name="scheduledTimeUtc">The time at which to start the operation.</param>
        /// <param name="operationName">The name of the operation.</param>
        /// <param name="operationInput">The input for the operation.</param>
        /// <param name="taskHubName">The TaskHubName of the target entity.</param>
        /// <param name="connectionName">The name of the connection string associated with <paramref name="taskHubName"/>.</param>
        /// <returns>A task that completes when the message has been reliably enqueued.</returns>
        Task SignalEntityAsync(EntityId entityId, DateTime scheduledTimeUtc, string operationName, object operationInput = null, string taskHubName = null, string connectionName = null);

        /// <summary>
        /// Tries to read the current state of an entity. Returns default(<typeparamref name="T"/>) if the entity does not
        /// exist, or if the JSON-serialized state of the entity is larger than 16KB.
        /// </summary>
        /// <typeparam name="T">The JSON-serializable type of the entity.</typeparam>
        /// <param name="entityId">The target entity.</param>
        /// <param name="taskHubName">The TaskHubName of the target entity.</param>
        /// <param name="connectionName">The name of the connection string associated with <paramref name="taskHubName"/>.</param>
        /// <returns>a response containing the current state of the entity.</returns>
        Task<EntityStateResponse<T>> ReadEntityStateAsync<T>(EntityId entityId, string taskHubName = null, string connectionName = null);
    }
}
