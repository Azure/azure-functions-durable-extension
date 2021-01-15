// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
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
        /// Signals an entity to perform an operation.
        /// </summary>
        /// <typeparam name="TEntityInterface">Entity interface.</typeparam>
        /// <param name="entityKey">The target entity key.</param>
        /// <param name="operation">A delegate that performs the desired operation on the entity.</param>
        /// <returns>A task that completes when the message has been reliably enqueued.</returns>
        Task SignalEntityAsync<TEntityInterface>(string entityKey, Action<TEntityInterface> operation);

        /// <summary>
        /// Signals an entity to perform an operation, at a specified time.
        /// </summary>
        /// <typeparam name="TEntityInterface">Entity interface.</typeparam>
        /// <param name="entityKey">The target entity key.</param>
        /// <param name="scheduledTimeUtc">The time at which to start the operation.</param>
        /// <param name="operation">A delegate that performs the desired operation on the entity.</param>
        /// <returns>A task that completes when the message has been reliably enqueued.</returns>
        Task SignalEntityAsync<TEntityInterface>(string entityKey, DateTime scheduledTimeUtc, Action<TEntityInterface> operation);

        /// <summary>
        /// Signals an entity to perform an operation.
        /// </summary>
        /// <typeparam name="TEntityInterface">Entity interface.</typeparam>
        /// <param name="entityId">The target entity.</param>
        /// <param name="operation">A delegate that performs the desired operation on the entity.</param>
        /// <returns>A task that completes when the message has been reliably enqueued.</returns>
        Task SignalEntityAsync<TEntityInterface>(EntityId entityId, Action<TEntityInterface> operation);

        /// <summary>
        /// Signals an entity to perform an operation, at a specified time.
        /// </summary>
        /// <typeparam name="TEntityInterface">Entity interface.</typeparam>
        /// <param name="entityId">The target entity.</param>
        /// <param name="scheduledTimeUtc">The time at which to start the operation.</param>
        /// <param name="operation">A delegate that performs the desired operation on the entity.</param>
        /// <returns>A task that completes when the message has been reliably enqueued.</returns>
        Task SignalEntityAsync<TEntityInterface>(EntityId entityId, DateTime scheduledTimeUtc, Action<TEntityInterface> operation);

        /// <summary>
        /// Tries to read the current state of an entity. Returns default(<typeparamref name="T"/>) if the entity does not exist.
        /// </summary>
        /// <typeparam name="T">The JSON-serializable type of the entity.</typeparam>
        /// <param name="entityId">The target entity.</param>
        /// <param name="taskHubName">The TaskHubName of the target entity.</param>
        /// <param name="connectionName">The name of the connection string associated with <paramref name="taskHubName"/>.</param>
        /// <returns>a response containing the current state of the entity.</returns>
        Task<EntityStateResponse<T>> ReadEntityStateAsync<T>(EntityId entityId, string taskHubName = null, string connectionName = null);

        /// <summary>
        /// Gets the status of all entity instances with paging that match the specified query conditions.
        /// </summary>
        /// <param name="query">Return entity instances that match the specified query conditions.</param>
        /// <param name="cancellationToken">Cancellation token that can be used to cancel the query operation.</param>
        /// <returns>Returns a page of entity instances and a continuation token for fetching the next page.</returns>
        Task<EntityQueryResult> ListEntitiesAsync(EntityQuery query, CancellationToken cancellationToken);

        /// <summary>
        /// Removes empty entities from storage and releases orphaned locks.
        /// </summary>
        /// <remarks>An entity is considered empty, and is removed, if it has no state, is not locked, and has
        /// been idle for more than <see cref="DurableTaskOptions.EntityMessageReorderWindowInMinutes"/> minutes.
        /// Locks are considered orphaned, and are released, if the orchestration that holds them is not in state <see cref="OrchestrationRuntimeStatus.Running"/>. This
        /// should not happen under normal circumstances, but can occur if the orchestration instance holding the lock
        /// exhibits replay nondeterminism failures, or if it is explicitly purged.</remarks>
        /// <param name="removeEmptyEntities">Whether to remove empty entities.</param>
        /// <param name="releaseOrphanedLocks">Whether to release orphaned locks.</param>
        /// <param name="cancellationToken">Cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task that completes when the operation is finished.</returns>
        Task<CleanEntityStorageResult> CleanEntityStorageAsync(bool removeEmptyEntities, bool releaseOrphanedLocks, CancellationToken cancellationToken);
    }
}
