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
using Newtonsoft.Json;
using AzureStorage = DurableTask.AzureStorage;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// The Azure Storage implementation of additional methods not required by IOrchestrationService.
    /// </summary>
    internal class AzureStorageDurabilityProvider : DurabilityProvider
    {
        private readonly AzureStorageOrchestrationService serviceClient;
        private readonly string connectionName;

        public AzureStorageDurabilityProvider(AzureStorageOrchestrationService service, string connectionName)
            : base("Azure Storage", service, service, connectionName)
        {
            this.serviceClient = service;
            this.connectionName = connectionName;
        }

        public override bool SupportsEntities => true;

        /// <summary>
        /// The app setting containing the Azure Storage connection string.
        /// </summary>
        public override string ConnectionName => this.connectionName;

        /// <inheritdoc/>
        public async override Task<IList<OrchestrationState>> GetAllOrchestrationStates(CancellationToken cancellationToken)
        {
            return await this.serviceClient.GetOrchestrationStateAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async override Task<IList<OrchestrationState>> GetOrchestrationStateConfigureInputsAsync(string instanceId, bool showInput)
        {
            return await this.serviceClient.GetOrchestrationStateAsync(instanceId, allExecutions: false, fetchInput: showInput);
        }

        /// <inheritdoc/>
        public async override Task RewindAsync(string instanceId, string reason)
        {
            await this.serviceClient.RewindTaskOrchestrationAsync(instanceId, reason);
        }

        /// <inheritdoc/>
        public async override Task<IList<OrchestrationState>> GetAllOrchestrationStatesWithFilters(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationRuntimeStatus> runtimeStatus, CancellationToken cancellationToken)
        {
            return await this.serviceClient.GetOrchestrationStateAsync(createdTimeFrom, createdTimeTo, runtimeStatus.Select(x => (OrchestrationStatus)x), cancellationToken);
        }

        /// <inheritdoc/>
        public async override Task<string> RetrieveSerializedEntityState(EntityId entityId)
        {
            var instanceId = EntityId.GetSchedulerIdFromEntityId(entityId);
            IList<OrchestrationState> stateList = await this.serviceClient.GetOrchestrationStateAsync(instanceId, false);

            OrchestrationState state = stateList?.FirstOrDefault();
            if (state != null
                && state.OrchestrationInstance != null
                && state.Input != null)
            {
                string serializedState;

                if (state.Input.StartsWith("http"))
                {
                    serializedState = await this.serviceClient.DownloadBlobAsync(state.Input);
                }
                else
                {
                    serializedState = state.Input;
                }

                var schedulerState = JsonConvert.DeserializeObject<SchedulerState>(serializedState, MessagePayloadDataConverter.MessageSettings);

                if (schedulerState.EntityExists)
                {
                    return schedulerState.EntityState;
                }
            }

            return null;
        }

        /// <inheritdoc/>
        public async override Task<PurgeHistoryResult> PurgeInstanceHistoryByInstanceId(string instanceId)
        {
            AzureStorage.PurgeHistoryResult purgeHistoryResult =
                await this.serviceClient.PurgeInstanceHistoryAsync(instanceId);
            return new PurgeHistoryResult(purgeHistoryResult.InstancesDeleted);
        }

        /// <inheritdoc/>
        public async override Task<int> PurgeHistoryByFilters(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationStatus> runtimeStatus)
        {
            AzureStorage.PurgeHistoryResult purgeHistoryResult =
                await this.serviceClient.PurgeInstanceHistoryAsync(createdTimeFrom, createdTimeTo, runtimeStatus);
            return purgeHistoryResult.InstancesDeleted;
        }

        /// <inheritdoc/>
        public async override Task<OrchestrationStatusQueryResult> GetOrchestrationStateWithPagination(OrchestrationStatusQueryCondition condition, CancellationToken cancellationToken)
        {
            var statusContext = await this.serviceClient.GetOrchestrationStateAsync(ConvertWebjobsDurableConditionToAzureStorageCondition(condition), condition.PageSize, condition.ContinuationToken, cancellationToken);
            return this.ConvertFrom(statusContext);
        }

        private OrchestrationStatusQueryResult ConvertFrom(DurableStatusQueryResult statusContext)
        {
            var results = new List<DurableOrchestrationStatus>();
            foreach (var state in statusContext.OrchestrationState)
            {
                results.Add(DurableClient.ConvertOrchestrationStateToStatus(state));
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
