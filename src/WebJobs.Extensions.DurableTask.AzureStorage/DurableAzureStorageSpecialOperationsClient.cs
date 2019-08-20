// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.AzureStorage;
using DurableTask.AzureStorage.Tracking;
using DurableTask.Core;
using AzureStorage = DurableTask.AzureStorage;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Azure
{
    /// <summary>
    /// The Azure Storage implementation of additional methods not required by IOrchestrationService.
    /// </summary>
    public class DurableAzureStorageSpecialOperationsClient : IDurableSpecialOperationsClient
    {
        private readonly TaskHubClient client;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        public DurableAzureStorageSpecialOperationsClient(TaskHubClient client)
        {
            this.client = client;
        }

        /// <inheritdoc/>
        public async Task<IList<OrchestrationState>> GetAllOrchestrationStates(CancellationToken cancellationToken)
        {
            // TODO this cast is to avoid to adding methods to the core IOrchestrationService/Client interface in DurableTask.Core. Eventually we will need
            // a better way of handling this
            var serviceClient = this.client.ServiceClient as AzureStorageOrchestrationService;
            if (serviceClient == null)
            {
                throw GetInvalidServiceClientException(nameof(GetAllOrchestrationStates));
            }

            return await serviceClient.GetOrchestrationStateAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<IList<OrchestrationState>> GetOrchestrationStateAsync(string instanceId, bool showHistory, bool showInput = true)
        {
            // TODO this cast is to avoid to adding methods to the core IOrchestrationService/Client interface in DurableTask.Core. Eventually we will need
            // a better way of handling this
            if (this.client.ServiceClient is AzureStorageOrchestrationService serviceClient)
            {
                return await serviceClient.GetOrchestrationStateAsync(instanceId, allExecutions: false, fetchInput: showInput);
            }
            else
            {
                throw GetInvalidServiceClientException(nameof(GetOrchestrationStateAsync));
            }
        }

        /// <inheritdoc/>
        public async Task RewindAsync(string instanceId, string reason)
        {
            // TODO this cast is to avoid to adding methods to the core IOrchestrationService/Client interface in DurableTask.Core. Eventually we will need
            // a better way of handling this
            var service = this.client.ServiceClient as AzureStorageOrchestrationService;
            if (service == null)
            {
                throw GetInvalidServiceClientException(nameof(RewindAsync));
            }

            await service.RewindTaskOrchestrationAsync(instanceId, reason);
        }

        private static Exception GetInvalidServiceClientException(string operationName)
        {
            throw new InvalidOperationException($"Cannot use a {nameof(TaskHubClient)} with a {nameof(TaskHubClient.ServiceClient)} other than {nameof(AzureStorageOrchestrationService)} for {operationName}.");
        }

        /// <inheritdoc/>
        public async Task<IList<OrchestrationState>> GetAllOrchestrationStatesWithFilters(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationRuntimeStatus> runtimeStatus, CancellationToken cancellationToken)
        {
            // TODO this cast is to avoid to adding methods to the core IOrchestrationService/Client interface in DurableTask.Core. Eventually we will need
            // a better way of handling this
            var serviceClient = this.client.ServiceClient as AzureStorageOrchestrationService;
            if (serviceClient == null)
            {
                throw GetInvalidServiceClientException(nameof(GetAllOrchestrationStatesWithFilters));
            }

            return await serviceClient.GetOrchestrationStateAsync(createdTimeFrom, createdTimeTo, runtimeStatus.Select(x => (OrchestrationStatus)x), cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<string> RetrieveSerializedEntityState(string inputState)
        {
            if (this.client.ServiceClient is AzureStorageOrchestrationService service)
            {
                // the input was compressed... read it back from blob
                return await service.DownloadBlobAsync(inputState);
            }
            else
            {
                throw GetInvalidServiceClientException(nameof(RetrieveSerializedEntityState));
            }
        }

        /// <inheritdoc/>
        public async Task<int> PurgeInstanceHistoryByInstanceId(string instanceId)
        {
            // TODO this cast is to avoid to adding methods to the core IOrchestrationService/Client interface in DurableTask.Core. Eventually we will need
            // a better way of handling this
            var serviceClient = this.client.ServiceClient as AzureStorageOrchestrationService;
            if (serviceClient == null)
            {
                throw GetInvalidServiceClientException(nameof(PurgeInstanceHistoryByInstanceId));
            }

            AzureStorage.PurgeHistoryResult purgeHistoryResult =
                await serviceClient.PurgeInstanceHistoryAsync(instanceId);
            return purgeHistoryResult.InstancesDeleted;
        }

        /// <inheritdoc/>
        public async Task<int> PurgeHistoryByFilters(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationStatus> runtimeStatus)
        {
            // TODO this cast is to avoid to adding methods to the core IOrchestrationService/Client interface in DurableTask.Core. Eventually we will need
            // a better way of handling this
            var serviceClient = this.client.ServiceClient as AzureStorageOrchestrationService;
            if (serviceClient == null)
            {
                throw GetInvalidServiceClientException(nameof(PurgeHistoryByFilters));
            }

            AzureStorage.PurgeHistoryResult purgeHistoryResult =
                await serviceClient.PurgeInstanceHistoryAsync(createdTimeFrom, createdTimeTo, runtimeStatus);
            return purgeHistoryResult.InstancesDeleted;
        }

        /// <inheritdoc/>
        public async Task<OrchestrationStatusQueryResult> GetOrchestrationStateWithPagination(OrchestrationStatusQueryCondition condition, CancellationToken cancellationToken)
        {
            // TODO this cast is to avoid to adding methods to the core IOrchestrationService/Client interface in DurableTask.Core. Eventually we will need
            // a better way of handling this
            var serviceClient = this.client.ServiceClient as AzureStorageOrchestrationService;
            if (serviceClient == null)
            {
                throw GetInvalidServiceClientException(nameof(GetOrchestrationStateWithPagination));
            }

            var statusContext = await serviceClient.GetOrchestrationStateAsync(ConvertWebjobsDurableConditionToAzureStorageCondition(condition), condition.PageSize, condition.ContinuationToken, cancellationToken);
            return this.ConvertFrom(statusContext);
        }

        private OrchestrationStatusQueryResult ConvertFrom(DurableStatusQueryResult statusContext)
        {
            var results = new List<DurableOrchestrationStatus>();
            foreach (var state in statusContext.OrchestrationState)
            {
                results.Add(OrchestrationStateConverter.ConvertOrchestrationStateToStatus(state));
            }

            var result = new OrchestrationStatusQueryResult
            {
                DurableOrchestrationState = results,
                ContinuationToken = statusContext.ContinuationToken,
            };

            return result;
        }

        internal static OrchestrationInstanceStatusQueryCondition ConvertWebjobsDurableConditionToAzureStorageCondition(OrchestrationStatusQueryCondition condition)
        {
            return new OrchestrationInstanceStatusQueryCondition
            {
                RuntimeStatus = condition.RuntimeStatus.Select(
                    p => (OrchestrationStatus)Enum.Parse(typeof(OrchestrationStatus), p.ToString())),
                CreatedTimeFrom = condition.CreatedTimeFrom,
                CreatedTimeTo = condition.CreatedTimeTo,
                TaskHubNames = condition.TaskHubNames,
            };
        }
    }
}
