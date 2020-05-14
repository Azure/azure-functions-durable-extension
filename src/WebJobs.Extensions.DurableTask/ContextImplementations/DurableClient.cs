// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
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

        private readonly TaskHubClient taskHubClient;
        private readonly string hubName;
        private readonly DurabilityProvider durabilityProvider;
        private readonly HttpApiHandler httpApiHandler;
        private readonly EndToEndTraceHelper traceHelper;
        private readonly DurableTaskExtension config;
        private readonly DurableClientAttribute attribute; // for rehydrating a Client after a webhook
        private readonly MessagePayloadDataConverter messageDataConverter;

        internal DurableClient(
            DurabilityProvider serviceClient,
            DurableTaskExtension config,
            HttpApiHandler httpHandler,
            DurableClientAttribute attribute)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));

            this.messageDataConverter = config.MessageDataConverter;

            this.taskHubClient = new TaskHubClient(serviceClient, this.messageDataConverter);
            this.durabilityProvider = serviceClient;
            this.traceHelper = config.TraceHelper;
            this.httpApiHandler = httpHandler;
            this.hubName = attribute.TaskHub ?? config.Options.HubName;
            this.attribute = attribute;
        }

        public string TaskHubName => this.hubName;

        internal DurabilityProvider DurabilityProvider => this.durabilityProvider;

        /// <inheritdoc />
        string IDurableOrchestrationClient.TaskHubName => this.TaskHubName;

        /// <inheritdoc />
        HttpResponseMessage IDurableOrchestrationClient.CreateCheckStatusResponse(HttpRequestMessage request, string instanceId, bool returnInternalServerErrorOnFailure)
        {
            return this.CreateCheckStatusResponse(request, instanceId, this.attribute, returnInternalServerErrorOnFailure);
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

        // Get a data structure containing status, terminate and send external event HTTP.
        internal HttpManagementPayload CreateHttpManagementPayload(
            string instanceId,
            string taskHubName,
            string connectionName)
        {
            return this.httpApiHandler.CreateHttpManagementPayload(instanceId, taskHubName, connectionName);
        }

        /// <inheritdoc />
        async Task<HttpResponseMessage> IDurableOrchestrationClient.WaitForCompletionOrCreateCheckStatusResponseAsync(HttpRequestMessage request, string instanceId, TimeSpan timeout, TimeSpan retryInterval)
        {
            return await this.WaitForCompletionOrCreateCheckStatusResponseAsync(
                request,
                instanceId,
                this.attribute,
                timeout,
                retryInterval);
        }

        // Get a response that will wait for response from the durable function for predefined period of time before
        // pointing to our webhook handler.
        internal async Task<HttpResponseMessage> WaitForCompletionOrCreateCheckStatusResponseAsync(
            HttpRequestMessage request,
            string instanceId,
            DurableClientAttribute attribute,
            TimeSpan timeout,
            TimeSpan retryInterval)
        {
            return await this.httpApiHandler.WaitForCompletionOrCreateCheckStatusResponseAsync(
                request,
                instanceId,
                attribute,
                timeout,
                retryInterval);
        }

        /// <inheritdoc />
        async Task<IActionResult> IDurableOrchestrationClient.WaitForCompletionOrCreateCheckStatusResponseAsync(HttpRequest request, string instanceId, TimeSpan timeout, TimeSpan retryInterval)
        {
            HttpRequestMessage requestMessage = ConvertHttpRequestMessage(request);
            HttpResponseMessage responseMessage = await ((IDurableOrchestrationClient)this).WaitForCompletionOrCreateCheckStatusResponseAsync(requestMessage, instanceId, timeout, retryInterval);
            return ConvertHttpResponseMessage(responseMessage);
        }

        private static IActionResult ConvertHttpResponseMessage(HttpResponseMessage response)
        {
            var result = new ObjectResult(response);
            result.Formatters.Add(new HttpResponseMessageOutputFormatter());
            return result;
        }

        private static HttpRequestMessage ConvertHttpRequestMessage(HttpRequest request)
        {
            return new HttpRequestMessageFeature(request.HttpContext).HttpRequestMessage;
        }

        /// <inheritdoc />
        Task<string> IDurableOrchestrationClient.StartNewAsync<T>(string orchestratorFunctionName, string instanceId, T input)
        {
            return ((IDurableOrchestrationClient)this).StartNewAsync<T>(orchestratorFunctionName, instanceId, input, null);
        }

        /// <inheritdoc />
        async Task<string> IDurableOrchestrationClient.StartNewAsync<T>(string orchestratorFunctionName, string instanceId, T input, DurableConnectionDetails connectionDetails)
        {
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

            var durableClient = this.GetDurableClient(connectionDetails);

            if (this.ClientReferencesCurrentApp(durableClient))
            {
                this.config.ThrowIfFunctionDoesNotExist(orchestratorFunctionName, FunctionType.Orchestrator);
            }

            var dedupeStatuses = this.GetStatusesNotToOverride();
            Task<OrchestrationInstance> createTask = durableClient.taskHubClient.CreateOrchestrationInstanceAsync(
                orchestratorFunctionName, DefaultVersion, instanceId, input, null, dedupeStatuses);

            this.traceHelper.FunctionScheduled(
                durableClient.TaskHubName,
                orchestratorFunctionName,
                instanceId,
                reason: "NewInstance",
                functionType: FunctionType.Orchestrator,
                isReplay: false);

            OrchestrationInstance instance = await createTask;
            return instance.InstanceId;
        }

        private static bool IsInvalidCharacter(char c)
        {
            return c == '/' || c == '\\' || c == '?' || c == '#' || char.IsControl(c);
        }

        private OrchestrationStatus[] GetStatusesNotToOverride()
        {
            var overridableStates = this.config.Options.OverridableExistingInstanceStates;
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

        Task IDurableOrchestrationClient.RaiseEventAsync(string instanceId, string eventName, object eventData)
        {
            return ((IDurableOrchestrationClient)this).RaiseEventAsync(instanceId, eventName, eventData, null);
        }

        /// <inheritdoc />
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate", Justification = "This method does not work with the .NET Framework event model.")]
        Task IDurableOrchestrationClient.RaiseEventAsync(string instanceId, string eventName, object eventData, DurableConnectionDetails connectionDetails)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                throw new ArgumentNullException(nameof(eventName));
            }

            return this.RaiseEventInternalAsync(connectionDetails, instanceId, eventName, eventData);
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

            var connectionDetails = new DurableConnectionDetails()
            {
                TaskHub = taskHubName,
                ConnectionName = connectionName,
            };

            return this.RaiseEventInternalAsync(connectionDetails, instanceId, eventName, eventData);
        }

        private async Task RaiseEventInternalAsync(
            DurableConnectionDetails connectionDetails,
            string instanceId,
            string eventName,
            object eventData)
        {
            var durableClient = this.GetDurableClient(connectionDetails);

            OrchestrationState status = await this.GetOrchestrationInstanceStateAsync(instanceId, durableClient);
            if (status == null)
            {
                return;
            }

            if (IsOrchestrationRunning(status))
            {
                // External events are not supposed to target any particular execution ID.
                // We need to clear it to avoid sending messages to an expired ContinueAsNew instance.
                status.OrchestrationInstance.ExecutionId = null;

                await durableClient.taskHubClient.RaiseEventAsync(status.OrchestrationInstance, eventName, eventData);

                this.traceHelper.FunctionScheduled(
                    durableClient.TaskHubName,
                    status.Name,
                    instanceId,
                    reason: "RaiseEvent:" + eventName,
                    functionType: FunctionType.Orchestrator,
                    isReplay: false);
            }
            else
            {
                this.traceHelper.ExtensionWarningEvent(
                    hubName: durableClient.TaskHubName,
                    functionName: status.Name,
                    instanceId: instanceId,
                    message: $"Cannot raise event for instance in {status.OrchestrationStatus} state");
                throw new InvalidOperationException($"Cannot raise event {eventName} for orchestration instance {instanceId} because instance is in {status.OrchestrationStatus} state");
            }
        }

        /// <inheritdoc />
        Task IDurableOrchestrationClient.TerminateAsync(string instanceId, string reason)
        {
            return ((IDurableOrchestrationClient)this).TerminateAsync(instanceId, reason, null);
        }

        /// <inheritdoc />
        async Task IDurableOrchestrationClient.TerminateAsync(string instanceId, string reason, DurableConnectionDetails connectionDetails)
        {
            var durableClient = this.GetDurableClient(connectionDetails);

            OrchestrationState state = await this.GetOrchestrationInstanceStateAsync(instanceId, durableClient);
            if (IsOrchestrationRunning(state))
            {
                // Terminate events are not supposed to target any particular execution ID.
                // We need to clear it to avoid sending messages to an expired ContinueAsNew instance.
                state.OrchestrationInstance.ExecutionId = null;

                await durableClient.taskHubClient.TerminateInstanceAsync(state.OrchestrationInstance, reason);

                this.traceHelper.FunctionTerminated(this.TaskHubName, state.Name, instanceId, reason);
            }
            else
            {
                this.traceHelper.ExtensionWarningEvent(
                    hubName: durableClient.TaskHubName,
                    functionName: state.Name,
                    instanceId: instanceId,
                    message: $"Cannot terminate orchestration instance in {state.Status} state");
                throw new InvalidOperationException($"Cannot terminate the orchestration instance {instanceId} because instance is in {state.Status} state");
            }
        }

        private static bool IsOrchestrationRunning(OrchestrationState status)
        {
            return status.OrchestrationStatus == OrchestrationStatus.Running ||
                status.OrchestrationStatus == OrchestrationStatus.Pending ||
                status.OrchestrationStatus == OrchestrationStatus.ContinuedAsNew;
        }

        /// <inheritdoc />
        Task IDurableOrchestrationClient.RewindAsync(string instanceId, string reason)
        {
            return ((IDurableOrchestrationClient)this).RewindAsync(instanceId, reason, null);
        }

        /// <inheritdoc />
        async Task IDurableOrchestrationClient.RewindAsync(string instanceId, string reason, DurableConnectionDetails connectionDetails)
        {
            var durableClient = this.GetDurableClient(connectionDetails);

            OrchestrationState state = await this.GetOrchestrationInstanceStateAsync(instanceId, durableClient);
            if (state.OrchestrationStatus != OrchestrationStatus.Failed)
            {
                throw new InvalidOperationException("The rewind operation is only supported on failed orchestration instances.");
            }

            await this.DurabilityProvider.RewindAsync(instanceId, reason);

            this.traceHelper.FunctionRewound(durableClient.TaskHubName, state.Name, instanceId, reason);
        }

        private async Task<OrchestrationState> GetOrchestrationInstanceStateAsync(string instanceId, DurableClient durableClient)
        {
            var client = durableClient.taskHubClient;

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

        /// <inheritdoc />
        Task<PurgeHistoryResult> IDurableOrchestrationClient.PurgeInstanceHistoryAsync(string instanceId)
        {
            return ((IDurableOrchestrationClient)this).PurgeInstanceHistoryAsync(instanceId, null);
        }

        /// <inheritdoc />
        Task<PurgeHistoryResult> IDurableOrchestrationClient.PurgeInstanceHistoryAsync(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationStatus> runtimeStatus)
        {
            return ((IDurableOrchestrationClient)this).PurgeInstanceHistoryAsync(createdTimeFrom, createdTimeTo, runtimeStatus, null);
        }

        /// <inheritdoc />
        async Task<PurgeHistoryResult> IDurableOrchestrationClient.PurgeInstanceHistoryAsync(string instanceId, DurableConnectionDetails connectionDetails)
        {
            var durableClient = this.GetDurableClient(connectionDetails);
            return await durableClient.DurabilityProvider.PurgeInstanceHistoryByInstanceId(instanceId);
        }

        /// <inheritdoc />
        async Task<PurgeHistoryResult> IDurableOrchestrationClient.PurgeInstanceHistoryAsync(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationStatus> runtimeStatus, DurableConnectionDetails connectionDetails)
        {
            var durableClient = this.GetDurableClient(connectionDetails);
            int numInstancesDeleted = await durableClient.DurabilityProvider.PurgeHistoryByFilters(createdTimeFrom, createdTimeTo, runtimeStatus);
            return new PurgeHistoryResult(numInstancesDeleted);
        }

        /// <inheritdoc />
        Task<DurableOrchestrationStatus> IDurableOrchestrationClient.GetStatusAsync(string instanceId, bool showHistory, bool showHistoryOutput, bool showInput)
        {
            return ((IDurableOrchestrationClient)this).GetStatusAsync(instanceId, showHistory, showHistoryOutput, showInput, null);
        }

        /// <inheritdoc />
        async Task<DurableOrchestrationStatus> IDurableOrchestrationClient.GetStatusAsync(string instanceId, bool showHistory, bool showHistoryOutput, bool showInput, DurableConnectionDetails connectionDetails)
        {
            var durableClient = this.GetDurableClient(connectionDetails);

            IList<OrchestrationState> stateList;
            try
            {
                stateList = await durableClient.DurabilityProvider.GetOrchestrationStateWithInputsAsync(instanceId, showInput);
            }
            catch (NotImplementedException)
            {
                // TODO: Ignore the show input flag. Should consider logging a warning.
                stateList = await durableClient.taskHubClient.ServiceClient.GetOrchestrationStateAsync(instanceId, allExecutions: false);
            }

            OrchestrationState state = stateList?.FirstOrDefault();
            if (state == null || state.OrchestrationInstance == null)
            {
                return null;
            }

            return await GetDurableOrchestrationStatusAsync(state, durableClient.taskHubClient, showHistory, showHistoryOutput);
        }

        private static async Task<DurableOrchestrationStatus> GetDurableOrchestrationStatusAsync(OrchestrationState orchestrationState, TaskHubClient client, bool showHistory, bool showHistoryOutput)
        {
            JArray historyArray = null;
            if (showHistory && orchestrationState.OrchestrationStatus != OrchestrationStatus.Pending)
            {
                string history = await client.GetOrchestrationHistoryAsync(orchestrationState.OrchestrationInstance);
                if (!string.IsNullOrEmpty(history))
                {
                    historyArray = JArray.Parse(history);

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

        /// <inheritdoc />
        Task<IList<DurableOrchestrationStatus>> IDurableOrchestrationClient.GetStatusAsync(CancellationToken cancellationToken)
        {
            return ((IDurableOrchestrationClient)this).GetStatusAsync(cancellationToken, null);
        }

        /// <inheritdoc />
        async Task<IList<DurableOrchestrationStatus>> IDurableOrchestrationClient.GetStatusAsync(CancellationToken cancellationToken, DurableConnectionDetails connectionDetails)
        {
            return await this.GetAllStatusHelper(connectionDetails, null, null, null, cancellationToken);
        }

        /// <inheritdoc />
        Task<IList<DurableOrchestrationStatus>> IDurableOrchestrationClient.GetStatusAsync(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationRuntimeStatus> runtimeStatus, CancellationToken cancellationToken)
        {
            return ((IDurableOrchestrationClient)this).GetStatusAsync(createdTimeFrom, createdTimeTo, runtimeStatus, cancellationToken, null);
        }

        /// <inheritdoc />
        async Task<IList<DurableOrchestrationStatus>> IDurableOrchestrationClient.GetStatusAsync(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationRuntimeStatus> runtimeStatus, CancellationToken cancellationToken, DurableConnectionDetails connectionDetails)
        {
            return await this.GetAllStatusHelper(connectionDetails, createdTimeFrom, createdTimeTo, runtimeStatus, cancellationToken);
        }

        private async Task<IList<DurableOrchestrationStatus>> GetAllStatusHelper(DurableConnectionDetails connectionDetails, DateTime? createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationRuntimeStatus> runtimeStatus, CancellationToken cancellationToken)
        {
            var condition = this.CreateConditionFromParameters(createdTimeFrom, createdTimeTo, runtimeStatus);

            var response = await ((IDurableOrchestrationClient)this).ListInstancesAsync(condition, cancellationToken, connectionDetails);

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

        /// <inheritdoc />
        Task<OrchestrationStatusQueryResult> IDurableOrchestrationClient.GetStatusAsync(OrchestrationStatusQueryCondition condition, CancellationToken cancellationToken)
        {
            return ((IDurableOrchestrationClient)this).GetStatusAsync(condition, cancellationToken, null);
        }

        /// <inheritdoc />
        Task<OrchestrationStatusQueryResult> IDurableOrchestrationClient.GetStatusAsync(
            OrchestrationStatusQueryCondition condition,
            CancellationToken cancellationToken,
            DurableConnectionDetails connectionDetails)
        {
            return ((IDurableOrchestrationClient)this).ListInstancesAsync(condition, cancellationToken, connectionDetails);
        }

        Task<OrchestrationStatusQueryResult> IDurableOrchestrationClient.ListInstancesAsync(OrchestrationStatusQueryCondition condition, CancellationToken cancellationToken)
        {
            return ((IDurableOrchestrationClient)this).ListInstancesAsync(condition, cancellationToken, null);
        }

        /// <inheritdoc />
        Task<OrchestrationStatusQueryResult> IDurableOrchestrationClient.ListInstancesAsync(
            OrchestrationStatusQueryCondition condition,
            CancellationToken cancellationToken,
            DurableConnectionDetails connectionDetails)
        {
            var durableClient = this.GetDurableClient(connectionDetails);
            return durableClient.DurabilityProvider.GetOrchestrationStateWithPagination(condition, cancellationToken);
        }

        /// <inheritdoc />
        Task IDurableEntityClient.SignalEntityAsync(EntityId entityId, string operationName, object operationInput, string taskHubName, string connectionName)
        {
            var connectionDetails = new DurableConnectionDetails()
            {
                TaskHub = taskHubName,
                ConnectionName = connectionName,
            };

            return this.SignalEntityAsyncInternal(connectionDetails, entityId, null, operationName, operationInput);
        }

        /// <inheritdoc />
        Task IDurableEntityClient.SignalEntityAsync(EntityId entityId, DateTime scheduledTimeUtc, string operationName, object operationInput, string taskHubName, string connectionName)
        {
            var connectionDetails = new DurableConnectionDetails()
            {
                TaskHub = taskHubName,
                ConnectionName = connectionName,
            };

            return this.SignalEntityAsyncInternal(connectionDetails, entityId, scheduledTimeUtc, operationName, operationInput);
        }

        private async Task SignalEntityAsyncInternal(DurableConnectionDetails connectionDetails, EntityId entityId, DateTime? scheduledTimeUtc, string operationName, object operationInput)
        {
            if (operationName == null)
            {
                throw new ArgumentNullException(nameof(operationName));
            }

            var durableClient = this.GetDurableClient(connectionDetails);

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
            var eventName = scheduledTimeUtc.HasValue ? EntityMessageEventNames.ScheduledRequestMessageEventName(scheduledTimeUtc.Value) : EntityMessageEventNames.RequestMessageEventName;
            await durableClient.taskHubClient.RaiseEventAsync(instance, eventName, jrequest);

            this.traceHelper.FunctionScheduled(
                connectionDetails?.TaskHub ?? this.TaskHubName,
                entityId.EntityName,
                EntityId.GetSchedulerIdFromEntityId(entityId),
                reason: $"EntitySignal:{operationName}",
                functionType: FunctionType.Entity,
                isReplay: false);
        }

        /// <inheritdoc />
        Task<EntityStateResponse<T>> IDurableEntityClient.ReadEntityStateAsync<T>(EntityId entityId, string taskHubName, string connectionName)
        {
            var connectionDetails = new DurableConnectionDetails
            {
                TaskHub = taskHubName,
                ConnectionName = connectionName,
            };

            return this.ReadEntityStateAsyncInternal<T>(connectionDetails, entityId);
        }

        private async Task<EntityStateResponse<T>> ReadEntityStateAsyncInternal<T>(DurableConnectionDetails connectionDetails, EntityId entityId)
        {
            var durableClient = this.GetDurableClient(connectionDetails);

            string entityState = await durableClient.DurabilityProvider.RetrieveSerializedEntityState(entityId, this.messageDataConverter.JsonSettings);

            return new EntityStateResponse<T>()
            {
                EntityExists = entityState != null,
                EntityState = this.messageDataConverter.Deserialize<T>(entityState),
            };
        }

        /// <inheritdoc />
        Task<EntityQueryResult> IDurableEntityClient.ListEntitiesAsync(EntityQuery query, CancellationToken cancellationToken)
        {
            return ((IDurableEntityClient)this).ListEntitiesAsync(query, cancellationToken, null);
        }

        /// <inheritdoc />
        async Task<EntityQueryResult> IDurableEntityClient.ListEntitiesAsync(EntityQuery query, CancellationToken cancellationToken, DurableConnectionDetails connectionDetails)
        {
            var condition = new OrchestrationStatusQueryCondition(query);
            var result = await ((IDurableClient)this).ListInstancesAsync(condition, cancellationToken, connectionDetails);
            var entityResult = new EntityQueryResult(result);
            return entityResult;
        }

        private DurableClient GetDurableClient(DurableConnectionDetails connectionDetails)
        {
            var attribute = new DurableClientAttribute
            {
                TaskHub = this.TaskHubName,
                ConnectionName = this.attribute.ConnectionName,
            };

            if (connectionDetails != null && !string.IsNullOrEmpty(connectionDetails.TaskHub))
            {
                attribute.TaskHub = connectionDetails.TaskHub;
            }

            if (connectionDetails != null && !string.IsNullOrEmpty(connectionDetails.ConnectionName))
            {
                attribute.ConnectionName = connectionDetails.ConnectionName;
            }

            return (DurableClient)this.config.GetClient(attribute);
        }

        private bool ClientReferencesCurrentApp(DurableClient client)
        {
            return this.TaskHubMatchesCurrentApp(client) && this.ConnectionNameMatchesCurrentApp(client);
        }

        private bool TaskHubMatchesCurrentApp(DurableClient client)
        {
            var taskHubName = this.config.Options.HubName;
            return client.TaskHubName.Equals(taskHubName);
        }

        private bool ConnectionNameMatchesCurrentApp(DurableClient client)
        {
            return this.config.DurabilityProvider.ConnectionNameMatches(client.DurabilityProvider);
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
                return JToken.Parse(value);
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

        private class EventIndexDateMapping
        {
            public int Index { get; set; }

            public DateTime Date { get; set; }

            public string Name { get; set; }
        }
    }
}
