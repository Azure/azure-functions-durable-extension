// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// An interface for methods that are not required to be implemented by standard Durable Task storage providers.
    /// </summary>
    public interface IDurableSpecialOperationsClient
    {
        /// <summary>
        /// Rewinds the specified failed orchestration instance with a reason.
        /// </summary>
        /// <param name="instanceId">The ID of the orchestration instance to rewind.</param>
        /// <param name="reason">The reason for rewinding the orchestration instance.</param>
        /// <returns>A task that completes when the rewind message is enqueued.</returns>
        Task RewindAsync(string instanceId, string reason);

        /// <summary>
        /// Gets the state of the specified orchestration instance.
        /// </summary>
        /// <param name="instanceId">The ID of the orchestration instance to query.</param>
        /// <param name="showHistory">Boolean marker for including execution history in the response.</param>
        /// <param name="showInput">If set, fetch and return the input for the orchestration instance.</param>
        /// <returns>Returns a task which completes when the state has been fetched.</returns>
        Task<IList<OrchestrationState>> GetOrchestrationStateAsync(string instanceId, bool showHistory,  bool showInput = true);

        /// <summary>
        /// Gets the status of all orchestration instances.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the request.</param>
        /// <returns>Returns a task which completes when the status has been fetched.</returns>
        Task<IList<OrchestrationState>> GetAllOrchestrationStates(CancellationToken cancellationToken);

        /// <summary>
        /// Gets the status of all orchestration instances within the specified parameters.
        /// </summary>
        /// <param name="createdTimeFrom">Return orchestration instances which were created after this DateTime.</param>
        /// <param name="createdTimeTo">Return orchestration instances which were created before this DateTime.</param>
        /// <param name="runtimeStatus">Return orchestration instances which matches the runtimeStatus.</param>
        /// <param name="cancellationToken">A token to cancel the request.</param>
        /// <returns>Returns a task which completes when the status has been fetched.</returns>
        Task<IList<OrchestrationState>> GetAllOrchestrationStatesWithFilters(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationRuntimeStatus> runtimeStatus, CancellationToken cancellationToken);

        /// <summary>
        /// Retrieves the state for a serialized entity.
        /// </summary>
        /// <param name="inputState">In memory representation of state.</param>
        /// <returns>Serialized state for the entity.</returns>
        Task<string> RetrieveSerializedEntityState(string inputState);

        /// <summary>
        /// Purges the instance history for the provided instance id.
        /// </summary>
        /// <param name="instanceId">The instance id for the instance history to purge.</param>
        /// <returns>The number of instances purged.</returns>
        Task<int> PurgeInstanceHistoryByInstanceId(string instanceId);

        /// <summary>
        /// Purges history that meet the required parameters.
        /// </summary>
        /// <param name="createdTimeFrom">Purge the history of orchestration instances which were created after this DateTime.</param>
        /// <param name="createdTimeTo">Purge the history of orchestration instances which were created before this DateTime.</param>
        /// <param name="runtimeStatus">Purge the history of orchestration instances which matches the runtimeStatus.</param>
        /// <returns>The number of instances purged.</returns>
        Task<int> PurgeHistoryByFilters(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationStatus> runtimeStatus);

        /// <summary>
        /// Gets paginated result of all orchestration instances that match query status parameters.
        /// </summary>
        /// <param name="condition">The filtering conditions of the query.</param>
        /// <param name="cancellationToken">A token to cancel the request.</param>
        /// <returns>Paginated result of orchestration state.</returns>
        Task<OrchestrationStatusQueryResult> GetOrchestrationStateWithPagination(OrchestrationStatusQueryCondition condition, CancellationToken cancellationToken);
    }
}
