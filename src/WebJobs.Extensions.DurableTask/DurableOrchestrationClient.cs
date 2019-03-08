// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.AzureStorage;
using DurableTask.Core;
using DurableTask.Core.History;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Client for starting, querying, terminating, and raising events to orchestration instances.
    /// </summary>
    public class DurableOrchestrationClient : DurableOrchestrationClientBase
    {
        private const string DefaultVersion = "";
        private const int MaxInstanceIdLength = 256;

        private static readonly JValue NullJValue = JValue.CreateNull();

        private readonly TaskHubClient client;
        private readonly string hubName;
        private readonly EndToEndTraceHelper traceHelper;
        private readonly DurableTaskExtension config;
        private readonly OrchestrationClientAttribute attribute; // for rehydrating a Client after a webhook

        internal DurableOrchestrationClient(
            IOrchestrationServiceClient serviceClient,
            DurableTaskExtension config,
            OrchestrationClientAttribute attribute)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));

            this.client = new TaskHubClient(serviceClient);
            this.traceHelper = config.TraceHelper;
            this.hubName = attribute.TaskHub ?? config.Options.HubName;
            this.attribute = attribute;
        }

        /// <inheritdoc />
        public override string TaskHubName => this.hubName;

        /// <inheritdoc />
        public override HttpResponseMessage CreateCheckStatusResponse(HttpRequestMessage request, string instanceId)
        {
            return this.config.CreateCheckStatusResponse(request, instanceId, this.attribute);
        }

        /// <inheritdoc />
        public override HttpManagementPayload CreateHttpManagementPayload(string instanceId)
        {
            return this.config.CreateHttpManagementPayload(instanceId, this.attribute.TaskHub, this.attribute.ConnectionName);
        }

        /// <inheritdoc />
        public override async Task<HttpResponseMessage> WaitForCompletionOrCreateCheckStatusResponseAsync(
            HttpRequestMessage request,
            string instanceId,
            TimeSpan timeout,
            TimeSpan retryInterval)
        {
            return await this.config.WaitForCompletionOrCreateCheckStatusResponseAsync(
                request,
                instanceId,
                this.attribute,
                timeout,
                retryInterval);
        }

        /// <inheritdoc />
        public override async Task<string> StartNewAsync(string orchestratorFunctionName, string instanceId, object input)
        {
            this.config.ThrowIfFunctionDoesNotExist(orchestratorFunctionName, FunctionType.Orchestrator);

            if (string.IsNullOrEmpty(instanceId))
            {
                instanceId = Guid.NewGuid().ToString("N");
            }

            if (instanceId.Length > MaxInstanceIdLength)
            {
                throw new ArgumentException($"Instance ID lengths must not exceed {MaxInstanceIdLength} characters.");
            }

            Task<OrchestrationInstance> createTask = this.client.CreateOrchestrationInstanceAsync(
                orchestratorFunctionName, DefaultVersion, instanceId, input);

            this.traceHelper.FunctionScheduled(
                this.hubName,
                orchestratorFunctionName,
                instanceId,
                reason: "NewInstance",
                functionType: FunctionType.Orchestrator,
                isReplay: false);

            OrchestrationInstance instance = await createTask;
            return instance.InstanceId;
        }

        /// <inheritdoc />
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate", Justification = "This method does not work with the .NET Framework event model.")]
        public override Task RaiseEventAsync(string instanceId, string eventName, object eventData)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                throw new ArgumentNullException(nameof(eventName));
            }

            return this.RaiseEventInternalAsync(this.client, this.hubName, instanceId, eventName, eventData);
        }

        /// <inheritdoc />
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate", Justification = "This method does not work with the .NET Framework event model.")]
        public override Task RaiseEventAsync(string taskHubName, string instanceId, string eventName, object eventData, string connectionName = null)
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

            var attribute = new OrchestrationClientAttribute
            {
                TaskHub = taskHubName,
                ConnectionName = connectionName,
            };

            TaskHubClient taskHubClient = this.config.GetClient(attribute).client;

            return this.RaiseEventInternalAsync(taskHubClient, taskHubName, instanceId, eventName, eventData);
        }

        /// <inheritdoc />
        public override async Task TerminateAsync(string instanceId, string reason)
        {
            OrchestrationState state = await this.GetOrchestrationInstanceAsync(instanceId);
            if (state.OrchestrationStatus == OrchestrationStatus.Running ||
                state.OrchestrationStatus == OrchestrationStatus.Pending ||
                state.OrchestrationStatus == OrchestrationStatus.ContinuedAsNew)
            {
                // Terminate events are not supposed to target any particular execution ID.
                // We need to clear it to avoid sending messages to an expired ContinueAsNew instance.
                state.OrchestrationInstance.ExecutionId = null;

                await this.client.TerminateInstanceAsync(state.OrchestrationInstance, reason);

                this.traceHelper.FunctionTerminated(this.hubName, state.Name, instanceId, reason);
            }
        }

        /// <inheritdoc />
        public override async Task RewindAsync(string instanceId, string reason)
        {
            OrchestrationState state = await this.GetOrchestrationInstanceAsync(instanceId);
            if (state.OrchestrationStatus != OrchestrationStatus.Failed)
            {
                throw new InvalidOperationException("The rewind operation is only supported on failed orchestration instances.");
            }

            var service = (AzureStorageOrchestrationService)this.client.ServiceClient;
            await service.RewindTaskOrchestrationAsync(instanceId, reason);

            this.traceHelper.FunctionRewound(this.hubName, state.Name, instanceId, reason);
        }

        /// <inheritdoc />
        public override async Task<DurableOrchestrationStatus> GetStatusAsync(string instanceId, bool showHistory = false, bool showHistoryOutput = false, bool showInput = true)
        {
            // TODO this cast is to avoid to change DurableTask.Core. Change it to use TaskHubClient.
            var storageService = (AzureStorageOrchestrationService)this.client.ServiceClient;
            IList<OrchestrationState> stateList = await storageService.GetOrchestrationStateAsync(instanceId, allExecutions: false, fetchInput: showInput);

            OrchestrationState state = stateList?.FirstOrDefault();
            if (state == null || state.OrchestrationInstance == null)
            {
                return null;
            }

            return await this.GetDurableOrchestrationStatusAsync(state, showHistory, showHistoryOutput);
        }

        /// <inheritdoc />
        public override async Task<IList<DurableOrchestrationStatus>> GetStatusAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            // TODO this cast is to avoid to change DurableTask.Core. Change it to use TaskHubClient.
            AzureStorageOrchestrationService serviceClient = (AzureStorageOrchestrationService)this.client.ServiceClient;
            IList<OrchestrationState> states = await serviceClient.GetOrchestrationStateAsync(cancellationToken);

            var results = new List<DurableOrchestrationStatus>();
            foreach (OrchestrationState state in states)
            {
                results.Add(this.ConvertFrom(state));
            }

            return results;
        }

        /// <inheritdoc />
        public override async Task<IList<DurableOrchestrationStatus>> GetStatusAsync(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationRuntimeStatus> runtimeStatus, CancellationToken cancellationToken = default(CancellationToken))
        {
            // TODO this cast is to avoid to change DurableTask.Core. Change it to use TaskHubClient.
            AzureStorageOrchestrationService serviceClient = (AzureStorageOrchestrationService)this.client.ServiceClient;
            IList<OrchestrationState> states = await serviceClient.GetOrchestrationStateAsync(createdTimeFrom, createdTimeTo, runtimeStatus.Select(x => (OrchestrationStatus)x), cancellationToken);
            var results = new List<DurableOrchestrationStatus>();
            foreach (OrchestrationState state in states)
            {
                results.Add(this.ConvertFrom(state));
            }

            return results;
        }

        /// <inheritdoc />
        public override async Task<PurgeHistoryResult> PurgeInstanceHistoryAsync(string instanceId)
        {
            // TODO this cast is to avoid to change DurableTask.Core. Change it to use TaskHubClient.
            AzureStorageOrchestrationService serviceClient = (AzureStorageOrchestrationService)this.client.ServiceClient;
            DurableTask.AzureStorage.PurgeHistoryResult purgeHistoryResult =
                await serviceClient.PurgeInstanceHistoryAsync(instanceId);
            return new PurgeHistoryResult(purgeHistoryResult.InstancesDeleted);
        }

        /// <inheritdoc />
        public override async Task<PurgeHistoryResult> PurgeInstanceHistoryAsync(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationStatus> runtimeStatus)
        {
            // TODO this cast is to avoid to change DurableTask.Core. Change it to use TaskHubClient.
            AzureStorageOrchestrationService serviceClient = (AzureStorageOrchestrationService)this.client.ServiceClient;
            DurableTask.AzureStorage.PurgeHistoryResult purgeHistoryResult =
                await serviceClient.PurgeInstanceHistoryAsync(createdTimeFrom, createdTimeTo, runtimeStatus);
            return new PurgeHistoryResult(purgeHistoryResult.InstancesDeleted);
        }

        /// <inheritdoc />
        public override async Task<OrchestrationStatusQueryResult> GetStatusAsync(
            DateTime createdTimeFrom,
            DateTime? createdTimeTo,
            IEnumerable<OrchestrationRuntimeStatus> runtimeStatus,
            int pageSize,
            string continuationToken,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var serviceClient = (AzureStorageOrchestrationService)this.client.ServiceClient;
            var statusContext = await serviceClient.GetOrchestrationStateAsync(createdTimeFrom, createdTimeTo, runtimeStatus.Select(x => (OrchestrationStatus)x), pageSize, continuationToken, cancellationToken);

            var results = new List<DurableOrchestrationStatus>();
            foreach (var state in statusContext.OrchestrationState)
            {
                results.Add(this.ConvertFrom(state));
            }

            var result = new OrchestrationStatusQueryResult
            {
                DurableOrchestrationState = results,
                ContinuationToken = statusContext.ContinuationToken,
            };

            return result;
        }

        private static JToken ParseToJToken(string value)
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

        private async Task<OrchestrationState> GetOrchestrationInstanceAsync(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                throw new ArgumentNullException(nameof(instanceId));
            }

            OrchestrationState state = await this.client.GetOrchestrationStateAsync(instanceId);
            if (state?.OrchestrationInstance == null)
            {
                throw new ArgumentException($"No instance with ID '{instanceId}' was found.", nameof(instanceId));
            }

            return state;
        }

        private async Task<DurableOrchestrationStatus> GetDurableOrchestrationStatusAsync(OrchestrationState orchestrationState, bool showHistory, bool showHistoryOutput)
        {
            JArray historyArray = null;
            if (showHistory && orchestrationState.OrchestrationStatus != OrchestrationStatus.Pending)
            {
                string history = await this.client.GetOrchestrationHistoryAsync(orchestrationState.OrchestrationInstance);
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

            return this.ConvertFrom(orchestrationState, historyArray);
        }

        private async Task RaiseEventInternalAsync(
            TaskHubClient taskHubClient,
            string taskHubName,
            string instanceId,
            string eventName,
            object eventData)
        {
            OrchestrationState status = await taskHubClient.GetOrchestrationStateAsync(instanceId);
            if (status == null)
            {
                return;
            }

            if (status.OrchestrationStatus == OrchestrationStatus.Running ||
                status.OrchestrationStatus == OrchestrationStatus.Pending ||
                status.OrchestrationStatus == OrchestrationStatus.ContinuedAsNew)
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
        }

        private DurableOrchestrationStatus ConvertFrom(OrchestrationState orchestrationState, JArray historyArray = null)
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
