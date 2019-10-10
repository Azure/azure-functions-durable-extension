// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.ContextImplementations
{
    /// <summary>
    /// An implementation of <see cref="IDurableSpecialOperationsClient"/> that throws user-friendly
    /// <see cref="NotImplementedException"/> messages by default. Storage providers that don't implement all of
    /// the special operations can use this, or extend this class and override the methods that can be
    /// implemented with that storage provider.
    /// </summary>
    public class DefaultDurableSpecialOperationsClient : IDurableSpecialOperationsClient
    {
        private readonly string storageProviderName;

        /// <summary>
        /// Creates an instance of the special operations client, with a storage provider name specified
        /// to make the exceptions thrown more clear.
        /// </summary>
        /// <param name="storageProviderName">Storage provider name to reference in <see cref="NotImplementedException"/> error messages.</param>
        public DefaultDurableSpecialOperationsClient(string storageProviderName)
        {
            this.storageProviderName = storageProviderName;
        }

        /// <inheritdoc/>
        public virtual Task<IList<OrchestrationState>> GetAllOrchestrationStates(CancellationToken cancellationToken)
        {
            throw this.GetNotImplementedException(nameof(this.GetAllOrchestrationStates));
        }

        /// <inheritdoc/>
        public virtual Task<IList<OrchestrationState>> GetAllOrchestrationStatesWithFilters(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationRuntimeStatus> runtimeStatus, CancellationToken cancellationToken)
        {
            throw this.GetNotImplementedException(nameof(this.GetAllOrchestrationStatesWithFilters));
        }

        /// <inheritdoc/>
        public virtual Task<IList<OrchestrationState>> GetOrchestrationStateAsync(string instanceId, bool showHistory, bool showInput = true)
        {
            throw this.GetNotImplementedException(nameof(this.GetOrchestrationStateAsync));
        }

        /// <inheritdoc/>
        public Task<OrchestrationStatusQueryResult> GetOrchestrationStateWithPagination(OrchestrationStatusQueryCondition condition, CancellationToken cancellationToken)
        {
            throw this.GetNotImplementedException(nameof(this.GetOrchestrationStateWithPagination));
        }

        /// <inheritdoc/>
        public Task<int> PurgeHistoryByFilters(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationStatus> runtimeStatus)
        {
            throw this.GetNotImplementedException(nameof(this.PurgeHistoryByFilters));
        }

        /// <inheritdoc/>
        public Task<int> PurgeInstanceHistoryByInstanceId(string instanceId)
        {
            throw this.GetNotImplementedException(nameof(this.PurgeInstanceHistoryByInstanceId));
        }

        /// <inheritdoc/>
        public Task<string> RetrieveSerializedEntityState(string inputState)
        {
            throw this.GetNotImplementedException(nameof(this.RetrieveSerializedEntityState));
        }

        /// <inheritdoc/>
        public Task RewindAsync(string instanceId, string reason)
        {
            throw this.GetNotImplementedException(nameof(this.RewindAsync));
        }

        private NotImplementedException GetNotImplementedException(string operationName)
        {
            return new NotImplementedException($"The {this.storageProviderName} storage provider does not support the {operationName} operation.");
        }
    }
}
