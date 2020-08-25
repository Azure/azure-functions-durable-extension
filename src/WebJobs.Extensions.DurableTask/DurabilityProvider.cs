// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// The backend storage provider that provides the actual durability of Durable Functions.
    /// This is functionally a superset of <see cref="IOrchestrationService"/> and <see cref="IOrchestrationServiceClient"/>.
    /// If the storage provider does not any of the Durable Functions specific operations, they can use this class
    /// directly with the expectation that only those interfaces will be implemented. All of the Durable Functions specific
    /// methods/operations are virtual and can be overwritten by creating a subclass.
    /// </summary>
    public class DurabilityProvider : IOrchestrationService, IOrchestrationServiceClient
    {
        internal const string NoConnectionDetails = "default";

        private static readonly JObject EmptyConfig = new JObject();

        private readonly string name;
        private readonly IOrchestrationService innerService;
        private readonly IOrchestrationServiceClient innerServiceClient;
        private readonly string connectionName;

        /// <summary>
        /// Creates the default <see cref="DurabilityProvider"/>.
        /// </summary>
        /// <param name="storageProviderName">The name of the storage backend providing the durability.</param>
        /// <param name="service">The internal <see cref="IOrchestrationService"/> that provides functionality
        /// for this classes implementions of <see cref="IOrchestrationService"/>.</param>
        /// <param name="serviceClient">The internal <see cref="IOrchestrationServiceClient"/> that provides functionality
        /// for this classes implementions of <see cref="IOrchestrationServiceClient"/>.</param>
        /// <param name="connectionName">The name of the app setting that stores connection details for the storage provider.</param>
        public DurabilityProvider(string storageProviderName, IOrchestrationService service, IOrchestrationServiceClient serviceClient, string connectionName)
        {
            this.name = storageProviderName ?? throw new ArgumentNullException(nameof(storageProviderName));
            this.innerService = service ?? throw new ArgumentNullException(nameof(service));
            this.innerServiceClient = serviceClient ?? throw new ArgumentNullException(nameof(serviceClient));
            this.connectionName = connectionName ?? throw new ArgumentNullException(connectionName);
        }

        /// <summary>
        /// The name of the environment variable that contains connection details for how to connect to storage providers.
        /// Corresponds to the <see cref="DurableClientAttribute.ConnectionName"/> for binding data.
        /// </summary>
        public virtual string ConnectionName => this.connectionName;

        /// <summary>
        /// Specifies whether the durability provider supports Durable Entities.
        /// </summary>
        public virtual bool SupportsEntities => false;

        /// <summary>
        /// JSON representation of configuration to emit in telemetry.
        /// </summary>
        public virtual JObject ConfigurationJson => EmptyConfig;

        /// <summary>
        /// Value of maximum durable timer delay. Used for long running durable timers.
        /// </summary>
        public virtual TimeSpan MaximumDelayTime { get; set; }

        /// <summary>
        /// Interval time used for long running timers.
        /// </summary>
        public virtual TimeSpan LongRunningTimerIntervalLength { get; set; }

        /// <inheritdoc/>
        public int TaskOrchestrationDispatcherCount => this.GetOrchestrationService().TaskOrchestrationDispatcherCount;

        /// <inheritdoc/>
        public int MaxConcurrentTaskOrchestrationWorkItems => this.GetOrchestrationService().MaxConcurrentTaskOrchestrationWorkItems;

        /// <inheritdoc/>
        public BehaviorOnContinueAsNew EventBehaviourForContinueAsNew => this.GetOrchestrationService().EventBehaviourForContinueAsNew;

        /// <inheritdoc/>
        public int TaskActivityDispatcherCount => this.GetOrchestrationService().TaskOrchestrationDispatcherCount;

        /// <inheritdoc/>
        public int MaxConcurrentTaskActivityWorkItems => this.GetOrchestrationService().MaxConcurrentTaskActivityWorkItems;

        private IOrchestrationService GetOrchestrationService()
        {
            if (this.innerService == null)
            {
                throw new NotSupportedException($"This storage provider was not provided an {nameof(IOrchestrationService)} instance, so it cannot call methods on that interface.");
            }

            return this.innerService;
        }

        private IOrchestrationServiceClient GetOrchestrationServiceClient()
        {
            if (this.innerServiceClient == null)
            {
                throw new NotSupportedException($"This storage provider was not provided an {nameof(IOrchestrationServiceClient)} instance, so it cannot call methods on that interface.");
            }

            return this.innerServiceClient;
        }

        /// <inheritdoc/>
        public Task AbandonTaskActivityWorkItemAsync(TaskActivityWorkItem workItem)
        {
            return this.GetOrchestrationService().AbandonTaskActivityWorkItemAsync(workItem);
        }

        /// <inheritdoc/>
        public Task AbandonTaskOrchestrationWorkItemAsync(TaskOrchestrationWorkItem workItem)
        {
            return this.GetOrchestrationService().AbandonTaskOrchestrationWorkItemAsync(workItem);
        }

        /// <inheritdoc/>
        public Task CompleteTaskActivityWorkItemAsync(TaskActivityWorkItem workItem, TaskMessage responseMessage)
        {
            return this.GetOrchestrationService().CompleteTaskActivityWorkItemAsync(workItem, responseMessage);
        }

        /// <inheritdoc/>
        public Task CompleteTaskOrchestrationWorkItemAsync(
            TaskOrchestrationWorkItem workItem,
            OrchestrationRuntimeState newOrchestrationRuntimeState,
            IList<TaskMessage> outboundMessages,
            IList<TaskMessage> orchestratorMessages,
            IList<TaskMessage> timerMessages,
            TaskMessage continuedAsNewMessage,
            OrchestrationState orchestrationState)
        {
            return this.GetOrchestrationService().CompleteTaskOrchestrationWorkItemAsync(
                workItem,
                newOrchestrationRuntimeState,
                outboundMessages,
                orchestratorMessages,
                timerMessages,
                continuedAsNewMessage,
                orchestrationState);
        }

        /// <inheritdoc/>
        public Task CreateAsync()
        {
            return this.GetOrchestrationService().CreateAsync();
        }

        /// <inheritdoc/>
        public Task CreateAsync(bool recreateInstanceStore)
        {
            return this.GetOrchestrationService().CreateAsync(recreateInstanceStore);
        }

        /// <inheritdoc/>
        public Task CreateIfNotExistsAsync()
        {
            return this.GetOrchestrationService().CreateIfNotExistsAsync();
        }

        /// <inheritdoc/>
        public Task DeleteAsync()
        {
            return this.GetOrchestrationService().DeleteAsync();
        }

        /// <inheritdoc/>
        public Task DeleteAsync(bool deleteInstanceStore)
        {
            return this.GetOrchestrationService().DeleteAsync(deleteInstanceStore);
        }

        /// <inheritdoc/>
        public int GetDelayInSecondsAfterOnFetchException(Exception exception)
        {
            return this.GetOrchestrationService().GetDelayInSecondsAfterOnFetchException(exception);
        }

        /// <inheritdoc/>
        public int GetDelayInSecondsAfterOnProcessException(Exception exception)
        {
            return this.GetOrchestrationService().GetDelayInSecondsAfterOnProcessException(exception);
        }

        /// <inheritdoc/>
        public bool IsMaxMessageCountExceeded(int currentMessageCount, OrchestrationRuntimeState runtimeState)
        {
            return this.GetOrchestrationService().IsMaxMessageCountExceeded(currentMessageCount, runtimeState);
        }

        /// <inheritdoc/>
        public Task<TaskActivityWorkItem> LockNextTaskActivityWorkItem(TimeSpan receiveTimeout, CancellationToken cancellationToken)
        {
            return this.GetOrchestrationService().LockNextTaskActivityWorkItem(receiveTimeout, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<TaskOrchestrationWorkItem> LockNextTaskOrchestrationWorkItemAsync(TimeSpan receiveTimeout, CancellationToken cancellationToken)
        {
            return this.GetOrchestrationService().LockNextTaskOrchestrationWorkItemAsync(receiveTimeout, cancellationToken);
        }

        /// <inheritdoc/>
        public Task ReleaseTaskOrchestrationWorkItemAsync(TaskOrchestrationWorkItem workItem)
        {
            return this.GetOrchestrationService().ReleaseTaskOrchestrationWorkItemAsync(workItem);
        }

        /// <inheritdoc/>
        public Task<TaskActivityWorkItem> RenewTaskActivityWorkItemLockAsync(TaskActivityWorkItem workItem)
        {
            return this.GetOrchestrationService().RenewTaskActivityWorkItemLockAsync(workItem);
        }

        /// <inheritdoc/>
        public Task RenewTaskOrchestrationWorkItemLockAsync(TaskOrchestrationWorkItem workItem)
        {
            return this.GetOrchestrationService().RenewTaskOrchestrationWorkItemLockAsync(workItem);
        }

        /// <inheritdoc/>
        public Task StartAsync()
        {
            return this.GetOrchestrationService().StartAsync();
        }

        /// <inheritdoc/>
        public Task StopAsync()
        {
            return this.GetOrchestrationService().StopAsync();
        }

        /// <inheritdoc/>
        public Task StopAsync(bool isForced)
        {
            return this.GetOrchestrationService().StopAsync(isForced);
        }

        private NotImplementedException GetNotImplementedException(string methodName)
        {
            return new NotImplementedException($"The method {methodName} is not supported by the {this.name} storage provider.");
        }

        /// <summary>
        /// Gets the status of all orchestration instances.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the request.</param>
        /// <returns>Returns a task which completes when the status has been fetched.</returns>
        public virtual Task<IList<OrchestrationState>> GetAllOrchestrationStates(CancellationToken cancellationToken)
        {
            throw this.GetNotImplementedException(nameof(this.GetAllOrchestrationStates));
        }

        /// <summary>
        /// Gets the status of all orchestration instances within the specified parameters.
        /// </summary>
        /// <param name="createdTimeFrom">Return orchestration instances which were created after this DateTime.</param>
        /// <param name="createdTimeTo">Return orchestration instances which were created before this DateTime.</param>
        /// <param name="runtimeStatus">Return orchestration instances which matches the runtimeStatus.</param>
        /// <param name="cancellationToken">A token to cancel the request.</param>
        /// <returns>Returns a task which completes when the status has been fetched.</returns>
        [Obsolete]
        public virtual Task<IList<OrchestrationState>> GetAllOrchestrationStatesWithFilters(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationRuntimeStatus> runtimeStatus, CancellationToken cancellationToken)
        {
            throw this.GetNotImplementedException(nameof(this.GetAllOrchestrationStatesWithFilters));
        }

        /// <summary>
        /// Gets the state of the specified orchestration instance.
        /// </summary>
        /// <param name="instanceId">The ID of the orchestration instance to query.</param>
        /// <param name="showInput">If set, fetch and return the input for the orchestration instance.</param>
        /// <returns>Returns a task which completes when the state has been fetched.</returns>
        public virtual Task<IList<OrchestrationState>> GetOrchestrationStateWithInputsAsync(string instanceId, bool showInput = true)
        {
            throw this.GetNotImplementedException(nameof(this.GetOrchestrationStateAsync));
        }

        /// <summary>
        /// Gets paginated result of all orchestration instances that match query status parameters.
        /// </summary>
        /// <param name="condition">The filtering conditions of the query.</param>
        /// <param name="cancellationToken">A token to cancel the request.</param>
        /// <returns>Paginated result of orchestration state.</returns>
        public virtual Task<OrchestrationStatusQueryResult> GetOrchestrationStateWithPagination(OrchestrationStatusQueryCondition condition, CancellationToken cancellationToken)
        {
            throw this.GetNotImplementedException(nameof(this.GetOrchestrationStateWithPagination));
        }

        /// <summary>
        /// Purges history that meet the required parameters.
        /// </summary>
        /// <param name="createdTimeFrom">Purge the history of orchestration instances which were created after this DateTime.</param>
        /// <param name="createdTimeTo">Purge the history of orchestration instances which were created before this DateTime.</param>
        /// <param name="runtimeStatus">Purge the history of orchestration instances which matches the runtimeStatus.</param>
        /// <returns>The number of instances purged.</returns>
        public virtual Task<int> PurgeHistoryByFilters(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationStatus> runtimeStatus)
        {
            throw this.GetNotImplementedException(nameof(this.PurgeHistoryByFilters));
        }

        /// <summary>
        /// Purges the instance history for the provided instance id.
        /// </summary>
        /// <param name="instanceId">The instance id for the instance history to purge.</param>
        /// <returns>The number of instances purged.</returns>
        public virtual Task<PurgeHistoryResult> PurgeInstanceHistoryByInstanceId(string instanceId)
        {
            throw this.GetNotImplementedException(nameof(this.PurgeInstanceHistoryByInstanceId));
        }

        /// <summary>
        /// Retrieves the state for a serialized entity.
        /// </summary>
        /// <param name="entityId">Entity id to fetch state for.</param>
        /// <param name="serializierSettings">JsonSerializerSettings for custom deserialization.</param>
        /// <returns>State for the entity.</returns>
        public virtual Task<string> RetrieveSerializedEntityState(EntityId entityId, JsonSerializerSettings serializierSettings)
        {
            throw this.GetNotImplementedException(nameof(this.RetrieveSerializedEntityState));
        }

        /// <summary>
        /// Rewinds the specified failed orchestration instance with a reason.
        /// </summary>
        /// <param name="instanceId">The ID of the orchestration instance to rewind.</param>
        /// <param name="reason">The reason for rewinding the orchestration instance.</param>
        /// <returns>A task that completes when the rewind message is enqueued.</returns>
        public virtual Task RewindAsync(string instanceId, string reason)
        {
            throw this.GetNotImplementedException(nameof(this.RewindAsync));
        }

        /// <inheritdoc />
        public Task CreateTaskOrchestrationAsync(TaskMessage creationMessage)
        {
            return this.GetOrchestrationServiceClient().CreateTaskOrchestrationAsync(creationMessage);
        }

        /// <inheritdoc />
        public Task CreateTaskOrchestrationAsync(TaskMessage creationMessage, OrchestrationStatus[] dedupeStatuses)
        {
            return this.GetOrchestrationServiceClient().CreateTaskOrchestrationAsync(creationMessage, dedupeStatuses);
        }

        /// <inheritdoc />
        public Task SendTaskOrchestrationMessageAsync(TaskMessage message)
        {
            return this.GetOrchestrationServiceClient().SendTaskOrchestrationMessageAsync(message);
        }

        /// <inheritdoc />
        public Task SendTaskOrchestrationMessageBatchAsync(params TaskMessage[] messages)
        {
            return this.GetOrchestrationServiceClient().SendTaskOrchestrationMessageBatchAsync(messages);
        }

        /// <inheritdoc />
        public Task<OrchestrationState> WaitForOrchestrationAsync(string instanceId, string executionId, TimeSpan timeout, CancellationToken cancellationToken)
        {
            return this.GetOrchestrationServiceClient().WaitForOrchestrationAsync(instanceId, executionId, timeout, cancellationToken);
        }

        /// <inheritdoc />
        public Task ForceTerminateTaskOrchestrationAsync(string instanceId, string reason)
        {
            return this.GetOrchestrationServiceClient().ForceTerminateTaskOrchestrationAsync(instanceId, reason);
        }

        /// <inheritdoc />
        public Task<IList<OrchestrationState>> GetOrchestrationStateAsync(string instanceId, bool allExecutions)
        {
            return this.GetOrchestrationServiceClient().GetOrchestrationStateAsync(instanceId, allExecutions);
        }

        /// <inheritdoc />
        public Task<OrchestrationState> GetOrchestrationStateAsync(string instanceId, string executionId)
        {
            return this.GetOrchestrationServiceClient().GetOrchestrationStateAsync(instanceId, executionId);
        }

        /// <inheritdoc />
        public Task<string> GetOrchestrationHistoryAsync(string instanceId, string executionId)
        {
            return this.GetOrchestrationServiceClient().GetOrchestrationHistoryAsync(instanceId, executionId);
        }

        /// <inheritdoc />
        public Task PurgeOrchestrationHistoryAsync(DateTime thresholdDateTimeUtc, OrchestrationStateTimeRangeFilterType timeRangeFilterType)
        {
            return this.GetOrchestrationServiceClient().PurgeOrchestrationHistoryAsync(thresholdDateTimeUtc, timeRangeFilterType);
        }

        /// <summary>
        /// Uses durability provider specific logic to verify whether a timespan for a timer, timeout
        /// or retry interval is allowed by the provider.
        /// </summary>
        /// <param name="timespan">The timespan that the code will have to wait for.</param>
        /// <param name="errorMessage">The error message if the timespan is invalid.</param>
        /// <returns>A boolean indicating whether the time interval is valid.</returns>
        public virtual bool ValidateDelayTime(TimeSpan timespan, out string errorMessage)
        {
            errorMessage = null;
            return true;
        }

        /// <summary>
        ///  Returns true if the stored connection string, ConnectionName, matches the input DurabilityProvider ConnectionName.
        /// </summary>
        /// <param name="durabilityProvider">The DurabilityProvider used to check for matching connection string names.</param>
        /// <returns>A boolean indicating whether the connection names match.</returns>
        internal virtual bool ConnectionNameMatches(DurabilityProvider durabilityProvider)
        {
            return this.ConnectionName.Equals(durabilityProvider.ConnectionName);
        }
    }
}
