// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.History;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.WebApiCompatShim;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Client for starting, querying, terminating, and raising events to orchestration instances.
    /// </summary>
    internal class DurableClient : IDurableClient,
#pragma warning disable 618
        DurableOrchestrationClientBase // for v1 legacy compatibility.
#pragma warning restore 618
    {
        private const string DefaultVersion = "";
        private const int MaxInstanceIdLength = 256;

        private static readonly JValue NullJValue = JValue.CreateNull();
        private static readonly OrchestrationRuntimeStatus[] RunningStatus
            = new OrchestrationRuntimeStatus[] { OrchestrationRuntimeStatus.Running };

        private readonly TaskHubClient client;
        private readonly string hubName;
        private readonly DurabilityProvider durabilityProvider;
        private readonly HttpApiHandler httpApiHandler;
        private readonly EndToEndTraceHelper traceHelper;
        private readonly DurableTaskExtension config;
        private readonly DurableClientAttribute attribute; // for rehydrating a Client after a webhook
        private readonly MessagePayloadDataConverter messageDataConverter;
        private readonly DurableTaskOptions durableTaskOptions;

        internal DurableClient(
            DurabilityProvider serviceClient,
            HttpApiHandler httpHandler,
            DurableClientAttribute attribute,
            MessagePayloadDataConverter messageDataConverter,
            EndToEndTraceHelper traceHelper,
            DurableTaskOptions durableTaskOptions)
        {
            this.messageDataConverter = messageDataConverter;

            this.client = new TaskHubClient(serviceClient, this.messageDataConverter);
            this.durabilityProvider = serviceClient;
            this.traceHelper = traceHelper;
            this.httpApiHandler = httpHandler;
            this.durableTaskOptions = durableTaskOptions;
            this.hubName = attribute.TaskHub ?? this.durableTaskOptions.HubName;
            this.attribute = attribute;
        }

        internal DurableClient(
            DurabilityProvider serviceClient,
            DurableTaskExtension config,
            HttpApiHandler httpHandler,
            DurableClientAttribute attribute)
            : this(serviceClient, httpHandler, attribute, config.MessageDataConverter, config.TraceHelper, config.Options)
        {
            this.config = config;
        }

        public string TaskHubName => this.hubName;

        internal DurabilityProvider DurabilityProvider => this.durabilityProvider;

        /// <inheritdoc />
        string IDurableOrchestrationClient.TaskHubName => this.TaskHubName;

        string IDurableClient.TaskHubName => this.TaskHubName;

        string IDurableEntityClient.TaskHubName => this.TaskHubName;

        /// <inheritdoc />
        HttpResponseMessage IDurableOrchestrationClient.CreateCheckStatusResponse(HttpRequestMessage request, string instanceId, bool returnInternalServerErrorOnFailure)
        {
            return this.CreateCheckStatusResponse(request, instanceId, this.attribute, returnInternalServerErrorOnFailure);
        }

        /// <inheritdoc />
        IActionResult IDurableOrchestrationClient.CreateCheckStatusResponse(HttpRequest request, string instanceId, bool returnInternalServerErrorOnFailure)
        {
            HttpRequestMessage requestMessage = ConvertHttpRequestMessage(request);
            HttpResponseMessage responseMessage = ((IDurableOrchestrationClient)this).CreateCheckStatusResponse(requestMessage, instanceId, returnInternalServerErrorOnFailure);
            return ConvertHttpResponseMessage(responseMessage);
        }

        /// <inheritdoc />
        HttpManagementPayload IDurableOrchestrationClient.CreateHttpManagementPayload(string instanceId)
        {
            return this.CreateHttpManagementPayload(instanceId, this.attribute.TaskHub, this.attribute.ConnectionName);
        }

        /// <inheritdoc />
        async Task<HttpResponseMessage> IDurableOrchestrationClient.WaitForCompletionOrCreateCheckStatusResponseAsync(HttpRequestMessage request, string instanceId, TimeSpan? timeout, TimeSpan? retryInterval, bool returnInternalServerErrorOnFailure)
        {
            return await this.WaitForCompletionOrCreateCheckStatusResponseAsync(
                request,
                instanceId,
                this.attribute,
                timeout ?? TimeSpan.FromSeconds(10),
                retryInterval ?? TimeSpan.FromSeconds(1),
                returnInternalServerErrorOnFailure);
        }

        /// <inheritdoc />
        async Task<IActionResult> IDurableOrchestrationClient.WaitForCompletionOrCreateCheckStatusResponseAsync(HttpRequest request, string instanceId, TimeSpan? timeout, TimeSpan? retryInterval, bool returnInternalServerErrorOnFailure)
        {
            HttpRequestMessage requestMessage = ConvertHttpRequestMessage(request);
            HttpResponseMessage responseMessage = await ((IDurableOrchestrationClient)this).WaitForCompletionOrCreateCheckStatusResponseAsync(requestMessage, instanceId, timeout, retryInterval, returnInternalServerErrorOnFailure);
            return ConvertHttpResponseMessage(responseMessage);
        }

        /// <inheritdoc />
        async Task<string> IDurableOrchestrationClient.StartNewAsync<T>(string orchestratorFunctionName, string instanceId, T input)
        {
            if (!this.attribute.ExternalClient && this.ClientReferencesCurrentApp(this))
            {
                this.config.ThrowIfFunctionDoesNotExist(orchestratorFunctionName, FunctionType.Orchestrator);
            }

            if (string.IsNullOrEmpty(instanceId))
            {
                instanceId = Guid.NewGuid().ToString("N");
            }
            else if (instanceId.StartsWith("@"))
            {
                throw new ArgumentException(nameof(instanceId), "Orchestration instance ids must not start with @.");
            }
            else if (instanceId.Any(IsInvalidCharacter))
            {
                throw new ArgumentException(nameof(instanceId), "Orchestration instance ids must not contain /, \\, #, ?, or control characters.");
            }

            if (instanceId.Length > MaxInstanceIdLength)
            {
                throw new ArgumentException($"Instance ID lengths must not exceed {MaxInstanceIdLength} characters.");
            }

            var dedupeStatuses = this.GetStatusesNotToOverride();
            Task<OrchestrationInstance> createTask = this.client.CreateOrchestrationInstanceAsync(
                orchestratorFunctionName, DefaultVersion, instanceId, input, null, dedupeStatuses);

            this.traceHelper.FunctionScheduled(
                this.TaskHubName,
                orchestratorFunctionName,
                instanceId,
                reason: "NewInstance",
                functionType: FunctionType.Orchestrator,
                isReplay: false);

            OrchestrationInstance instance = await createTask;
            return instance.InstanceId;
        }

        private OrchestrationStatus[] GetStatusesNotToOverride()
        {
            var overridableStates = this.durableTaskOptions.OverridableExistingInstanceStates;
            if (overridableStates == OverridableStates.NonRunningStates)
            {
                return new OrchestrationStatus[]
                {
                    OrchestrationStatus.Running,
                    OrchestrationStatus.ContinuedAsNew,
                    OrchestrationStatus.Pending,
                };
            }

            return new OrchestrationStatus[0];
        }

        private static bool IsInvalidCharacter(char c)
        {
            return c == '/' || c == '\\' || c == '?' || c == '#' || char.IsControl(c);
        }

        /// <inheritdoc />
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate", Justification = "This method does not work with the .NET Framework event model.")]
        Task IDurableOrchestrationClient.RaiseEventAsync(string instanceId, string eventName, object eventData)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                throw new ArgumentNullException(nameof(eventName));
            }

            return this.RaiseEventInternalAsync(this.client, this.TaskHubName, instanceId, eventName, eventData);
        }

        /// <inheritdoc />
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate", Justification = "This method does not work with the .NET Framework event model.")]
        Task IDurableOrchestrationClient.RaiseEventAsync(string taskHubName, string instanceId, string eventName, object eventData, string connectionName)
        {
            if (string.IsNullOrEmpty(taskHubName))
            {
                throw new ArgumentNullException(nameof(taskHubName));
            }

            if (string.IsNullOrEmpty(eventName))
            {
                throw new ArgumentNullException(nameof(eventName));
            }

            if (string.IsNullOrEmpty(connectionName))
            {
                connectionName = this.attribute.ConnectionName;
            }

            var attribute = new DurableClientAttribute
            {
                TaskHub = taskHubName,
                ConnectionName = connectionName,
            };

            TaskHubClient taskHubClient = ((DurableClient)this.config.GetClient(attribute)).client;

            return this.RaiseEventInternalAsync(taskHubClient, taskHubName, instanceId, eventName, eventData);
        }

        /// <inheritdoc />
        Task IDurableEntityClient.SignalEntityAsync(EntityId entityId, string operationName, object operationInput, string taskHubName, string connectionName)
        {
            if (string.IsNullOrEmpty(taskHubName))
            {
                return this.SignalEntityAsyncInternal(this, this.TaskHubName, entityId, null, operationName, operationInput);
            }
            else
            {
                if (string.IsNullOrEmpty(connectionName))
                {
                    connectionName = this.attribute.ConnectionName;
                }

                var attribute = new DurableClientAttribute
                {
                    TaskHub = taskHubName,
                    ConnectionName = connectionName,
                };

                var durableClient = (DurableClient)this.config.GetClient(attribute);
                return this.SignalEntityAsyncInternal(durableClient, taskHubName, entityId, null, operationName, operationInput);
            }
        }

        /// <inheritdoc />
        Task IDurableEntityClient.SignalEntityAsync(EntityId entityId, DateTime scheduledTimeUtc, string operationName, object operationInput, string taskHubName, string connectionName)
        {
            if (string.IsNullOrEmpty(taskHubName))
            {
                return this.SignalEntityAsyncInternal(this, this.TaskHubName, entityId, scheduledTimeUtc, operationName, operationInput);
            }
            else
            {
                if (string.IsNullOrEmpty(connectionName))
                {
                    connectionName = this.attribute.ConnectionName;
                }

                var attribute = new DurableClientAttribute
                {
                    TaskHub = taskHubName,
                    ConnectionName = connectionName,
                };

                var durableClient = (DurableClient)this.config.GetClient(attribute);
                return this.SignalEntityAsyncInternal(durableClient, taskHubName, entityId, scheduledTimeUtc, operationName, operationInput);
            }
        }

        private async Task SignalEntityAsyncInternal(DurableClient durableClient, string hubName, EntityId entityId, DateTime? scheduledTimeUtc, string operationName, object operationInput)
        {
            var entityKey = entityId.EntityKey;
            if (entityKey.Any(IsInvalidCharacter))
            {
                throw new ArgumentException(nameof(entityKey), "Entity keys must not contain /, \\, #, ?, or control characters.");
            }

            if (operationName == null)
            {
                throw new ArgumentNullException(nameof(operationName));
            }

            if (this.ClientReferencesCurrentApp(durableClient))
            {
                this.config.ThrowIfFunctionDoesNotExist(entityId.EntityName, FunctionType.Entity);
            }

            var guid = Guid.NewGuid(); // unique id for this request
            var instanceId = EntityId.GetSchedulerIdFromEntityId(entityId);
            var instance = new OrchestrationInstance() { InstanceId = instanceId };
            var request = new RequestMessage()
            {
                ParentInstanceId = null, // means this was sent by a client
                ParentExecutionId = null,
                Id = guid,
                IsSignal = true,
                Operation = operationName,
                ScheduledTime = scheduledTimeUtc,
            };
            if (operationInput != null)
            {
                request.SetInput(operationInput, this.messageDataConverter);
            }

            var jrequest = JToken.FromObject(request, this.messageDataConverter.JsonSerializer);
            var eventName = scheduledTimeUtc.HasValue
                ? EntityMessageEventNames.ScheduledRequestMessageEventName(request.GetAdjustedDeliveryTime(this.durabilityProvider))
                : EntityMessageEventNames.RequestMessageEventName;
            await durableClient.client.RaiseEventAsync(instance, eventName, jrequest);

            this.traceHelper.FunctionScheduled(
                hubName,
                entityId.EntityName,
                EntityId.GetSchedulerIdFromEntityId(entityId),
                reason: $"EntitySignal:{operationName}",
                functionType: FunctionType.Entity,
                isReplay: false);
        }

        private bool ClientReferencesCurrentApp(DurableClient client)
        {
            return this.TaskHubMatchesCurrentApp(client) && this.ConnectionNameMatchesCurrentApp(client);
        }

        private bool TaskHubMatchesCurrentApp(DurableClient client)
        {
            var taskHubName = this.durableTaskOptions.HubName;
            return client.TaskHubName.Equals(taskHubName);
        }

        private bool ConnectionNameMatchesCurrentApp(DurableClient client)
        {
            return this.DurabilityProvider.ConnectionNameMatches(client.DurabilityProvider);
        }

        /// <inheritdoc />
        async Task IDurableOrchestrationClient.TerminateAsync(string instanceId, string reason)
        {
            OrchestrationState state = await this.GetOrchestrationInstanceStateAsync(instanceId);
            if (IsOrchestrationRunning(state))
            {
                // Terminate events are not supposed to target any particular execution ID.
                // We need to clear it to avoid sending messages to an expired ContinueAsNew instance.
                state.OrchestrationInstance.ExecutionId = null;

                await this.client.TerminateInstanceAsync(state.OrchestrationInstance, reason);

                this.traceHelper.FunctionTerminated(this.TaskHubName, state.Name, instanceId, reason);
            }
            else
            {
                this.traceHelper.ExtensionWarningEvent(
                    hubName: this.TaskHubName,
                    functionName: state.Name,
                    instanceId: instanceId,
                    message: $"Cannot terminate orchestration instance in {state.Status} state");
                throw new InvalidOperationException($"Cannot terminate the orchestration instance {instanceId} because instance is in {state.Status} state");
            }
        }

        /// <inheritdoc />
        async Task IDurableOrchestrationClient.RewindAsync(string instanceId, string reason)
        {
            OrchestrationState state = await this.GetOrchestrationInstanceStateAsync(instanceId);
            if (state.OrchestrationStatus != OrchestrationStatus.Failed)
            {
                throw new InvalidOperationException("The rewind operation is only supported on failed orchestration instances.");
            }

            await this.DurabilityProvider.RewindAsync(instanceId, reason);

            this.traceHelper.FunctionRewound(this.TaskHubName, state.Name, instanceId, reason);
        }

        /// <inheritdoc />
        async Task<DurableOrchestrationStatus> IDurableOrchestrationClient.GetStatusAsync(string instanceId, bool showHistory, bool showHistoryOutput, bool showInput)
        {
            IList<OrchestrationState> stateList;
            try
            {
                stateList = await this.DurabilityProvider.GetOrchestrationStateWithInputsAsync(instanceId, showInput);
            }
            catch (NotImplementedException)
            {
                // TODO: Ignore the show input flag. Should consider logging a warning.
                stateList = await this.client.ServiceClient.GetOrchestrationStateAsync(instanceId, allExecutions: false);
            }

            OrchestrationState state = stateList?.FirstOrDefault();
            if (state == null || state.OrchestrationInstance == null)
            {
                return null;
            }

            return await GetDurableOrchestrationStatusAsync(state, this.client, showHistory, showHistoryOutput);
        }

        /// <inheritdoc />
        async Task<IList<DurableOrchestrationStatus>> IDurableOrchestrationClient.GetStatusAsync(DateTime? createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationRuntimeStatus> runtimeStatus, CancellationToken cancellationToken)
        {
            return await this.GetAllStatusHelper(createdTimeFrom, createdTimeTo, runtimeStatus, cancellationToken);
        }

        private async Task<IList<DurableOrchestrationStatus>> GetAllStatusHelper(DateTime? createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationRuntimeStatus> runtimeStatus, CancellationToken cancellationToken)
        {
            var condition = this.CreateConditionFromParameters(createdTimeFrom, createdTimeTo, runtimeStatus);

            var response = await ((IDurableOrchestrationClient)this).ListInstancesAsync(condition, cancellationToken);

            return (IList<DurableOrchestrationStatus>)response.DurableOrchestrationState;
        }

        private OrchestrationStatusQueryCondition CreateConditionFromParameters(DateTime? createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationRuntimeStatus> runtimeStatus)
        {
            var condition = new OrchestrationStatusQueryCondition();

            if (createdTimeFrom != null)
            {
                condition.CreatedTimeFrom = createdTimeFrom.Value;
            }

            if (createdTimeTo != null)
            {
                condition.CreatedTimeTo = createdTimeTo.Value;
            }

            if (runtimeStatus != null)
            {
                condition.RuntimeStatus = runtimeStatus;
            }

            return condition;
        }

        Task<EntityStateResponse<T>> IDurableEntityClient.ReadEntityStateAsync<T>(EntityId entityId, string taskHubName, string connectionName)
        {
            if (string.IsNullOrEmpty(taskHubName))
            {
                return this.ReadEntityStateAsync<T>(this.DurabilityProvider, entityId);
            }
            else
            {
                if (string.IsNullOrEmpty(connectionName))
                {
                    connectionName = this.attribute.ConnectionName;
                }

                var attribute = new DurableClientAttribute
                {
                    TaskHub = taskHubName,
                    ConnectionName = connectionName,
                };

                DurabilityProvider durabilityProvider = ((DurableClient)this.config.GetClient(attribute)).DurabilityProvider;
                return this.ReadEntityStateAsync<T>(durabilityProvider, entityId);
            }
        }

        private async Task<EntityStateResponse<T>> ReadEntityStateAsync<T>(DurabilityProvider provider, EntityId entityId)
        {
            string entityState = await provider.RetrieveSerializedEntityState(entityId, this.messageDataConverter.JsonSettings);

            return new EntityStateResponse<T>()
            {
                EntityExists = entityState != null,
                EntityState = this.messageDataConverter.Deserialize<T>(entityState),
            };
        }

        /// <inheritdoc />
        async Task<PurgeHistoryResult> IDurableOrchestrationClient.PurgeInstanceHistoryAsync(string instanceId)
        {
            return await this.DurabilityProvider.PurgeInstanceHistoryByInstanceId(instanceId);
        }

        /// <inheritdoc />
        async Task<PurgeHistoryResult> IDurableOrchestrationClient.PurgeInstanceHistoryAsync(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationStatus> runtimeStatus)
        {
            int numInstancesDeleted = await this.DurabilityProvider.PurgeHistoryByFilters(createdTimeFrom, createdTimeTo, runtimeStatus);
            return new PurgeHistoryResult(numInstancesDeleted);
        }

        Task<OrchestrationStatusQueryResult> IDurableOrchestrationClient.GetStatusAsync(
            OrchestrationStatusQueryCondition condition,
            CancellationToken cancellationToken)
        {
            return ((IDurableOrchestrationClient)this).ListInstancesAsync(condition, cancellationToken);
        }

        /// <inheritdoc />
        Task<OrchestrationStatusQueryResult> IDurableOrchestrationClient.ListInstancesAsync(
            OrchestrationStatusQueryCondition condition,
            CancellationToken cancellationToken)
        {
            return this.DurabilityProvider.GetOrchestrationStateWithPagination(condition, cancellationToken);
        }

        /// <inheritdoc />
        async Task<EntityQueryResult> IDurableEntityClient.ListEntitiesAsync(EntityQuery query, CancellationToken cancellationToken)
        {
            var condition = new OrchestrationStatusQueryCondition(query);
            var result = await ((IDurableClient)this).ListInstancesAsync(condition, cancellationToken);
            var entityResult = new EntityQueryResult(result);
            return entityResult;
        }

        /// <inheritdoc />
        async Task<CleanEntityStorageResult> IDurableEntityClient.CleanEntityStorageAsync(bool removeEmptyEntities, bool releaseOrphanedLocks, CancellationToken cancellationToken)
        {
            DateTime now = DateTime.UtcNow;
            CleanEntityStorageResult finalResult = default;

            var condition = new OrchestrationStatusQueryCondition()
            {
                InstanceIdPrefix = "@",
                ShowInput = false,
            };

            // list all entities (without fetching the input) and for each one that requires action,
            // perform that action. Waits for all actions to finish after each page.
            do
            {
                var page = await this.DurabilityProvider.GetOrchestrationStateWithPagination(condition, cancellationToken);

                List<Task> tasks = new List<Task>();
                foreach (var state in page.DurableOrchestrationState)
                {
                    EntityStatus status = this.messageDataConverter.Deserialize<EntityStatus>(state.CustomStatus.ToString());
                    if (releaseOrphanedLocks && status.LockedBy != null)
                    {
                         tasks.Add(CheckForOrphanedLockAndFixIt(state, status.LockedBy));
                    }

                    if (removeEmptyEntities && !status.EntityExists && status.LockedBy == null && status.QueueSize == 0
                        && now - state.LastUpdatedTime > TimeSpan.FromMinutes(this.config.Options.EntityMessageReorderWindowInMinutes))
                    {
                        tasks.Add(DeleteIdleOrchestrationEntity(state));
                    }
                }

                async Task DeleteIdleOrchestrationEntity(DurableOrchestrationStatus status)
                {
                    await this.DurabilityProvider.PurgeInstanceHistoryByInstanceId(status.InstanceId);
                    Interlocked.Increment(ref finalResult.NumberOfEmptyEntitiesRemoved);
                }

                async Task CheckForOrphanedLockAndFixIt(DurableOrchestrationStatus status, string lockOwner)
                {
                    var findRunningOwner = new OrchestrationStatusQueryCondition()
                    {
                        InstanceIdPrefix = lockOwner,
                        ShowInput = false,
                        RuntimeStatus = RunningStatus,
                    };
                    var result = await this.DurabilityProvider.GetOrchestrationStateWithPagination(findRunningOwner, cancellationToken);
                    if (!result.DurableOrchestrationState.Any(state => state.InstanceId == lockOwner))
                    {
                        // the owner is not a running orchestration. Send a lock release.
                        var message = new ReleaseMessage()
                        {
                            ParentInstanceId = lockOwner,
                            LockRequestId = "fix-orphaned-lock", // we don't know the original id but it does not matter
                        };
                        await this.RaiseEventInternalAsync(this.client, this.TaskHubName, status.InstanceId, EntityMessageEventNames.ReleaseMessageEventName, message);
                        Interlocked.Increment(ref finalResult.NumberOfOrphanedLocksRemoved);
                    }
                }

                await Task.WhenAll(tasks);
                condition.ContinuationToken = page.ContinuationToken;
            }
            while (condition.ContinuationToken != null);

            return finalResult;
        }

        private async Task<OrchestrationState> GetOrchestrationInstanceStateAsync(string instanceId)
        {
            return await GetOrchestrationInstanceStateAsync(this.client, instanceId);
        }

        private static async Task<OrchestrationState> GetOrchestrationInstanceStateAsync(TaskHubClient client, string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                throw new ArgumentNullException(nameof(instanceId));
            }

            OrchestrationState state = await client.GetOrchestrationStateAsync(instanceId);
            if (state?.OrchestrationInstance == null)
            {
                throw new ArgumentException($"No instance with ID '{instanceId}' was found.", nameof(instanceId));
            }

            return state;
        }

        private async Task RaiseEventInternalAsync(
            TaskHubClient taskHubClient,
            string taskHubName,
            string instanceId,
            string eventName,
            object eventData)
        {
            OrchestrationState status = await GetOrchestrationInstanceStateAsync(taskHubClient, instanceId);
            if (status == null)
            {
                return;
            }

            if (IsOrchestrationRunning(status))
            {
                // External events are not supposed to target any particular execution ID.
                // We need to clear it to avoid sending messages to an expired ContinueAsNew instance.
                status.OrchestrationInstance.ExecutionId = null;

                await taskHubClient.RaiseEventAsync(status.OrchestrationInstance, eventName, eventData);

                this.traceHelper.FunctionScheduled(
                    taskHubName,
                    status.Name,
                    instanceId,
                    reason: "RaiseEvent:" + eventName,
                    functionType: FunctionType.Orchestrator,
                    isReplay: false);
            }
            else
            {
                this.traceHelper.ExtensionWarningEvent(
                    hubName: taskHubName,
                    functionName: status.Name,
                    instanceId: instanceId,
                    message: $"Cannot raise event for instance in {status.OrchestrationStatus} state");
                throw new InvalidOperationException($"Cannot raise event {eventName} for orchestration instance {instanceId} because instance is in {status.OrchestrationStatus} state");
            }
        }

        // Get a data structure containing status, terminate and send external event HTTP.
        internal HttpManagementPayload CreateHttpManagementPayload(
            string instanceId,
            string taskHubName,
            string connectionName)
        {
            return this.httpApiHandler.CreateHttpManagementPayload(instanceId, taskHubName, connectionName);
        }

        // Get a response that will wait for response from the durable function for predefined period of time before
        // pointing to our webhook handler.
        internal async Task<HttpResponseMessage> WaitForCompletionOrCreateCheckStatusResponseAsync(
            HttpRequestMessage request,
            string instanceId,
            DurableClientAttribute attribute,
            TimeSpan timeout,
            TimeSpan retryInterval,
            bool returnInternalServerErrorOnFailure)
        {
            return await this.httpApiHandler.WaitForCompletionOrCreateCheckStatusResponseAsync(
                request,
                instanceId,
                attribute,
                timeout,
                retryInterval,
                returnInternalServerErrorOnFailure);
        }

        private static bool IsOrchestrationRunning(OrchestrationState status)
        {
            return status.OrchestrationStatus == OrchestrationStatus.Running ||
                status.OrchestrationStatus == OrchestrationStatus.Pending ||
                status.OrchestrationStatus == OrchestrationStatus.ContinuedAsNew;
        }

        private static HttpRequestMessage ConvertHttpRequestMessage(HttpRequest request)
        {
            return new HttpRequestMessageFeature(request.HttpContext).HttpRequestMessage;
        }

        private static IActionResult ConvertHttpResponseMessage(HttpResponseMessage response)
        {
            var result = new ObjectResult(response);
            result.Formatters.Add(new HttpResponseMessageOutputFormatter());
            return result;
        }

        private static async Task<DurableOrchestrationStatus> GetDurableOrchestrationStatusAsync(OrchestrationState orchestrationState, TaskHubClient client, bool showHistory, bool showHistoryOutput)
        {
            JArray historyArray = null;
            if (showHistory && orchestrationState.OrchestrationStatus != OrchestrationStatus.Pending)
            {
                string history = await client.GetOrchestrationHistoryAsync(orchestrationState.OrchestrationInstance);
                if (!string.IsNullOrEmpty(history))
                {
                    historyArray = MessagePayloadDataConverter.ConvertToJArray(history);

                    var eventMapper = new Dictionary<string, EventIndexDateMapping>();
                    var indexList = new List<int>();

                    for (var i = 0; i < historyArray.Count; i++)
                    {
                        JObject historyItem = (JObject)historyArray[i];
                        if (Enum.TryParse(historyItem["EventType"].Value<string>(), out EventType eventType))
                        {
                            // Changing the value of EventType from integer to string for better understanding in the history output
                            historyItem["EventType"] = eventType.ToString();
                            switch (eventType)
                            {
                                case EventType.TaskScheduled:
                                    TrackNameAndScheduledTime(historyItem, eventType, i, eventMapper);
                                    historyItem.Remove("Version");
                                    historyItem.Remove("Input");
                                    break;
                                case EventType.TaskCompleted:
                                case EventType.TaskFailed:
                                    AddScheduledEventDataAndAggregate(ref eventMapper, "TaskScheduled", historyItem, indexList);
                                    historyItem["TaskScheduledId"]?.Parent.Remove();
                                    if (!showHistoryOutput && eventType == EventType.TaskCompleted)
                                    {
                                        historyItem.Remove("Result");
                                    }

                                    ConvertOutputToJToken(historyItem, showHistoryOutput && eventType == EventType.TaskCompleted);
                                    break;
                                case EventType.SubOrchestrationInstanceCreated:
                                    TrackNameAndScheduledTime(historyItem, eventType, i, eventMapper);
                                    historyItem.Remove("Version");
                                    historyItem.Remove("Input");
                                    break;
                                case EventType.SubOrchestrationInstanceCompleted:
                                case EventType.SubOrchestrationInstanceFailed:
                                    AddScheduledEventDataAndAggregate(ref eventMapper, "SubOrchestrationInstanceCreated", historyItem, indexList);
                                    historyItem.Remove("TaskScheduledId");
                                    if (!showHistoryOutput && eventType == EventType.SubOrchestrationInstanceCompleted)
                                    {
                                        historyItem.Remove("Result");
                                    }

                                    ConvertOutputToJToken(historyItem, showHistoryOutput && eventType == EventType.SubOrchestrationInstanceCompleted);
                                    break;
                                case EventType.ExecutionStarted:
                                    var functionName = historyItem["Name"];
                                    historyItem.Remove("Name");
                                    historyItem["FunctionName"] = functionName;
                                    historyItem.Remove("OrchestrationInstance");
                                    historyItem.Remove("ParentInstance");
                                    historyItem.Remove("Version");
                                    historyItem.Remove("Tags");
                                    historyItem.Remove("Input");
                                    break;
                                case EventType.ExecutionCompleted:
                                    if (Enum.TryParse(historyItem["OrchestrationStatus"].Value<string>(), out OrchestrationStatus orchestrationStatus))
                                    {
                                        historyItem["OrchestrationStatus"] = orchestrationStatus.ToString();
                                    }

                                    if (!showHistoryOutput)
                                    {
                                        historyItem.Remove("Result");
                                    }

                                    ConvertOutputToJToken(historyItem, showHistoryOutput);
                                    break;
                                case EventType.ExecutionTerminated:
                                    historyItem.Remove("Input");
                                    break;
                                case EventType.TimerFired:
                                    historyItem.Remove("TimerId");
                                    break;
                                case EventType.EventRaised:
                                    historyItem.Remove("Input");
                                    break;
                                case EventType.OrchestratorStarted:
                                case EventType.OrchestratorCompleted:
                                    indexList.Add(i);
                                    break;
                            }

                            historyItem.Remove("EventId");
                            historyItem.Remove("IsPlayed");
                        }
                    }

                    var counter = 0;
                    indexList.Sort();
                    foreach (var indexValue in indexList)
                    {
                        historyArray.RemoveAt(indexValue - counter);
                        counter++;
                    }
                }
            }

            return ConvertOrchestrationStateToStatus(orchestrationState, historyArray);
        }

        // Get a response that will point to our webhook handler.
        internal HttpResponseMessage CreateCheckStatusResponse(
            HttpRequestMessage request,
            string instanceId,
            DurableClientAttribute attribute,
            bool returnInternalServerErrorOnFailure = false)
        {
            return this.httpApiHandler.CreateCheckStatusResponse(request, instanceId, attribute, returnInternalServerErrorOnFailure);
        }

        private static void TrackNameAndScheduledTime(JObject historyItem, EventType eventType, int index, Dictionary<string, EventIndexDateMapping> eventMapper)
        {
            eventMapper.Add($"{eventType}_{historyItem["EventId"]}", new EventIndexDateMapping { Index = index, Name = (string)historyItem["Name"], Date = (DateTime)historyItem["Timestamp"] });
        }

        private static void AddScheduledEventDataAndAggregate(ref Dictionary<string, EventIndexDateMapping> eventMapper, string prefix, JToken historyItem, List<int> indexList)
        {
            if (eventMapper.TryGetValue($"{prefix}_{historyItem["TaskScheduledId"]}", out EventIndexDateMapping taskScheduledData))
            {
                historyItem["ScheduledTime"] = taskScheduledData.Date;
                historyItem["FunctionName"] = taskScheduledData.Name;
                indexList.Add(taskScheduledData.Index);
            }
        }

        internal static DurableOrchestrationStatus ConvertOrchestrationStateToStatus(OrchestrationState orchestrationState, JArray historyArray = null)
        {
            return new DurableOrchestrationStatus
            {
                Name = orchestrationState.Name,
                InstanceId = orchestrationState.OrchestrationInstance.InstanceId,
                CreatedTime = orchestrationState.CreatedTime,
                LastUpdatedTime = orchestrationState.LastUpdatedTime,
                RuntimeStatus = (OrchestrationRuntimeStatus)orchestrationState.OrchestrationStatus,
                CustomStatus = ParseToJToken(orchestrationState.Status),
                Input = ParseToJToken(orchestrationState.Input),
                Output = ParseToJToken(orchestrationState.Output),
                History = historyArray,
            };
        }

        internal static JToken ParseToJToken(string value)
        {
            if (value == null)
            {
                return NullJValue;
            }

            // Ignore whitespace
            value = value.Trim();
            if (value.Length == 0)
            {
                return string.Empty;
            }

            try
            {
                return MessagePayloadDataConverter.ConvertToJToken(value);
            }
            catch (JsonReaderException)
            {
                // Return the raw string value as the fallback. This is common in terminate scenarios.
                return value;
            }
        }

        private static void ConvertOutputToJToken(JObject jsonObject, bool showHistoryOutput)
        {
            if (!showHistoryOutput)
            {
                return;
            }

            jsonObject["Result"] = ParseToJToken((string)jsonObject["Result"]);
        }

        /// <inheritdoc/>
        Task<string> IDurableOrchestrationClient.StartNewAsync(string orchestratorFunctionName, string instanceId)
        {
            return ((IDurableOrchestrationClient)this).StartNewAsync<object>(orchestratorFunctionName, instanceId, null);
        }

        /// <inheritdoc/>
        Task<string> IDurableOrchestrationClient.StartNewAsync<T>(string orchestratorFunctionName, T input)
        {
            return ((IDurableOrchestrationClient)this).StartNewAsync<T>(orchestratorFunctionName, string.Empty, input);
        }

        /// <inheritdoc/>
        Task IDurableEntityClient.SignalEntityAsync<TEntityInterface>(string entityKey, Action<TEntityInterface> operation)
        {
            return ((IDurableEntityClient)this).SignalEntityAsync<TEntityInterface>(new EntityId(DurableEntityProxyHelpers.ResolveEntityName<TEntityInterface>(), entityKey), operation);
        }

        /// <inheritdoc/>
        Task IDurableEntityClient.SignalEntityAsync<TEntityInterface>(string entityKey, DateTime scheduledTimeUtc, Action<TEntityInterface> operation)
        {
            return ((IDurableEntityClient)this).SignalEntityAsync<TEntityInterface>(new EntityId(DurableEntityProxyHelpers.ResolveEntityName<TEntityInterface>(), entityKey), scheduledTimeUtc, operation);
        }

        /// <inheritdoc/>
        Task IDurableEntityClient.SignalEntityAsync<TEntityInterface>(EntityId entityId, Action<TEntityInterface> operation)
        {
            var proxyContext = new EntityClientProxy(this);
            var proxy = EntityProxyFactory.Create<TEntityInterface>(proxyContext, entityId);

            operation(proxy);

            if (proxyContext.SignalTask == null)
            {
                throw new InvalidOperationException("The operation action must perform an operation on the entity");
            }

            return proxyContext.SignalTask;
        }

        /// <inheritdoc/>
        Task IDurableEntityClient.SignalEntityAsync<TEntityInterface>(EntityId entityId, DateTime scheduledTimeUtc, Action<TEntityInterface> operation)
        {
            var proxyContext = new EntityClientProxy(this, scheduledTimeUtc);
            var proxy = EntityProxyFactory.Create<TEntityInterface>(proxyContext, entityId);

            operation(proxy);

            if (proxyContext.SignalTask == null)
            {
                throw new InvalidOperationException("The operation action must perform an operation on the entity");
            }

            return proxyContext.SignalTask;
        }

        private class EventIndexDateMapping
        {
            public int Index { get; set; }

            public DateTime Date { get; set; }

            public string Name { get; set; }
        }
    }
}
