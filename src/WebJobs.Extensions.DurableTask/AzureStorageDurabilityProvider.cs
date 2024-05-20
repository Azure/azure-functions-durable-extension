﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.AzureStorage;
using DurableTask.AzureStorage.Tracking;
using DurableTask.Core;
<<<<<<< HEAD
using DurableTask.Core.Entities;
=======
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Storage;
>>>>>>> v3.x
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
#if !FUNCTIONS_V1
using Microsoft.Azure.WebJobs.Host.Scale;
#endif
using AzureStorage = DurableTask.AzureStorage;
using DTCore = DurableTask.Core;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// The Azure Storage implementation of additional methods not required by IOrchestrationService.
    /// </summary>
    internal class AzureStorageDurabilityProvider : DurabilityProvider
    {
        private readonly AzureStorageOrchestrationService serviceClient;
        private readonly IStorageServiceClientProviderFactory clientProviderFactory;
        private readonly string connectionName;
        private readonly JObject storageOptionsJson;
        private readonly ILogger logger;

        public AzureStorageDurabilityProvider(
            AzureStorageOrchestrationService service,
            IStorageServiceClientProviderFactory clientProviderFactory,
            string connectionName,
            AzureStorageOptions options,
            ILogger logger)
            : base("Azure Storage", service, service, connectionName)
        {
            this.serviceClient = service;
            this.clientProviderFactory = clientProviderFactory;
            this.connectionName = connectionName;
            this.storageOptionsJson = JObject.FromObject(
                options,
                new JsonSerializer
                {
                    Converters = { new StringEnumConverter() },
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                });
            this.logger = logger;
        }

        public override bool CheckStatusBeforeRaiseEvent => true;

        /// <summary>
        /// The app setting containing the Azure Storage connection string.
        /// </summary>
        public override string ConnectionName => this.connectionName;

        public override JObject ConfigurationJson => this.storageOptionsJson;

        public override TimeSpan MaximumDelayTime { get; set; } = TimeSpan.FromDays(6);

        public override TimeSpan LongRunningTimerIntervalLength { get; set; } = TimeSpan.FromDays(3);

        public override string EventSourceName { get; set; } = "DurableTask-AzureStorage";

        /// <inheritdoc/>
        public async override Task<IList<OrchestrationState>> GetAllOrchestrationStates(CancellationToken cancellationToken)
        {
            return await this.serviceClient.GetOrchestrationStateAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async override Task<IList<OrchestrationState>> GetOrchestrationStateWithInputsAsync(string instanceId, bool showInput = true)
        {
            return await this.serviceClient.GetOrchestrationStateAsync(instanceId, allExecutions: false, fetchInput: showInput);
        }

        /// <inheritdoc/>
        public async override Task RewindAsync(string instanceId, string reason)
        {
            await this.serviceClient.RewindTaskOrchestrationAsync(instanceId, reason);
        }

        /// <inheritdoc/>
        [Obsolete]
        public async override Task<IList<OrchestrationState>> GetAllOrchestrationStatesWithFilters(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationRuntimeStatus> runtimeStatus, CancellationToken cancellationToken)
        {
            return await this.serviceClient.GetOrchestrationStateAsync(createdTimeFrom, createdTimeTo, runtimeStatus.Select(x => (OrchestrationStatus)x), cancellationToken);
        }

        /// <inheritdoc/>
        public async override Task<string> RetrieveSerializedEntityState(EntityId entityId, JsonSerializerSettings serializerSettings)
        {
            EntityBackendQueries entityBackendQueries = (this.serviceClient as IEntityOrchestrationService)?.EntityBackendQueries;

            if (entityBackendQueries != null) // entity queries are natively supported
            {
                var entity = await entityBackendQueries.GetEntityAsync(new DTCore.Entities.EntityId(entityId.EntityName, entityId.EntityKey), cancellation: default);

                if (entity == null)
                {
                    return null;
                }
                else
                {
                    return entity.Value.SerializedState;
                }
            }
            else // fall back to old implementation
            {
                return await this.LegacyImplementationOfRetrieveSerializedEntityState(entityId, serializerSettings);
            }
        }

        private async Task<string> LegacyImplementationOfRetrieveSerializedEntityState(EntityId entityId, JsonSerializerSettings serializerSettings)
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

                var schedulerState = JsonConvert.DeserializeObject<SchedulerState>(serializedState, serializerSettings);

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

        public override bool ValidateDelayTime(TimeSpan timespan, out string errorMessage)
        {
            if (timespan > this.MaximumDelayTime)
            {
                errorMessage = $"The Azure Storage provider supports a maximum of {this.MaximumDelayTime.TotalDays} days for time-based delays";
                return false;
            }

            return base.ValidateDelayTime(timespan, out errorMessage);
        }

        /// <inheritdoc/>
        public async override Task MakeCurrentAppPrimaryAsync()
        {
            await this.serviceClient.ForceChangeAppLeaseAsync();
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
                RuntimeStatus = condition.RuntimeStatus?.Select(
                    p => (OrchestrationStatus)Enum.Parse(typeof(OrchestrationStatus), p.ToString())),
                CreatedTimeFrom = condition.CreatedTimeFrom,
                CreatedTimeTo = condition.CreatedTimeTo,
                TaskHubNames = condition.TaskHubNames,
                InstanceIdPrefix = condition.InstanceIdPrefix,
                FetchInput = condition.ShowInput,
            };
        }

#if !FUNCTIONS_V1

        internal DurableTaskMetricsProvider GetMetricsProvider(
<<<<<<< HEAD
            string functionName,
            string hubName,
            CloudStorageAccount storageAccount,
            ILogger logger)
        {
            return new DurableTaskMetricsProvider(functionName, hubName, logger, performanceMonitor: null, storageAccount);
=======
           string functionName,
           string hubName,
           StorageAccountClientProvider storageAccountClientProvider,
           ILogger logger)
        {
            return new DurableTaskMetricsProvider(functionName, hubName, logger, performanceMonitor: null, storageAccountClientProvider);
>>>>>>> v3.x
        }

        /// <inheritdoc/>
        public override bool TryGetScaleMonitor(
            string functionId,
            string functionName,
            string hubName,
            string connectionName,
            out IScaleMonitor scaleMonitor)
        {
<<<<<<< HEAD
            CloudStorageAccount storageAccount = this.storageAccountProvider.GetStorageAccountDetails(connectionName).ToCloudStorageAccount();
            DurableTaskMetricsProvider metricsProvider = this.GetMetricsProvider(functionName, hubName, storageAccount, this.logger);
=======
            StorageAccountClientProvider storageAccountClientProvider = this.clientProviderFactory.GetClientProvider(connectionName);
            DurableTaskMetricsProvider metricsProvider = this.GetMetricsProvider(functionName, hubName, storageAccountClientProvider, this.logger);
>>>>>>> v3.x
            scaleMonitor = new DurableTaskScaleMonitor(
                functionId,
                functionName,
                hubName,
<<<<<<< HEAD
                storageAccount,
=======
                storageAccountClientProvider,
>>>>>>> v3.x
                this.logger,
                metricsProvider);
            return true;
        }

#endif
#if FUNCTIONS_V3_OR_GREATER
        public override bool TryGetTargetScaler(
            string functionId,
            string functionName,
            string hubName,
            string connectionName,
            out ITargetScaler targetScaler)
        {
            // This is only called by the ScaleController, it doesn't run in the Functions Host process.
<<<<<<< HEAD
            CloudStorageAccount storageAccount = this.storageAccountProvider.GetStorageAccountDetails(connectionName).ToCloudStorageAccount();
            DurableTaskMetricsProvider metricsProvider = this.GetMetricsProvider(functionName, hubName, storageAccount, this.logger);
=======
            StorageAccountClientProvider storageAccountClientProvider = this.clientProviderFactory.GetClientProvider(connectionName);
            DurableTaskMetricsProvider metricsProvider = this.GetMetricsProvider(functionName, hubName, storageAccountClientProvider, this.logger);
>>>>>>> v3.x
            targetScaler = new DurableTaskTargetScaler(functionId, metricsProvider, this, this.logger);
            return true;
        }
#endif
    }
}
