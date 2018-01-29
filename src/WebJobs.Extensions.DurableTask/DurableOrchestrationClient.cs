// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.History;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Client for starting, querying, terminating, and raising events to new or existing orchestration instances.
    /// </summary>
    public class DurableOrchestrationClient : DurableOrchestrationClientBase
    {
        private const string DefaultVersion = "";

        private readonly TaskHubClient client;
        private readonly string hubName;
        private readonly EndToEndTraceHelper traceHelper;
        private readonly DurableTaskExtension config;
        private readonly OrchestrationClientAttribute attribute; // for rehydrating a Client after a webhook

        internal DurableOrchestrationClient(
            IOrchestrationServiceClient serviceClient,
            DurableTaskExtension config,
            OrchestrationClientAttribute attribute,
            EndToEndTraceHelper traceHelper)
        {
            this.client = new TaskHubClient(serviceClient);
            this.traceHelper = traceHelper;
            this.config = config;
            this.hubName = config.HubName;
            this.attribute = attribute;
        }

        /// <inheritdoc />
        public override HttpResponseMessage CreateCheckStatusResponse(HttpRequestMessage request, string instanceId)
        {
            return this.config.CreateCheckStatusResponse(request, instanceId, this.attribute);
        }

        /// <inheritdoc />
        public override async Task<HttpResponseMessage> WaitForCompletionOrCreateCheckStatusResponseAsync(HttpRequestMessage request, string instanceId, TimeSpan? timeout, TimeSpan? retryInterval)
        {
            return await this.config.WaitForCompletionOrCreateCheckStatusResponseAsync(request, instanceId, this.attribute, timeout ?? TimeSpan.FromSeconds(10), retryInterval ?? TimeSpan.FromSeconds(1));
        }

        /// <inheritdoc />
        public override async Task<string> StartNewAsync(string orchestratorFunctionName, string instanceId, object input)
        {
            this.config.AssertOrchestratorExists(orchestratorFunctionName, DefaultVersion);

            OrchestrationInstance instance = await this.client.CreateOrchestrationInstanceAsync(
                orchestratorFunctionName, DefaultVersion, instanceId, input);

            this.traceHelper.FunctionScheduled(
                this.hubName,
                orchestratorFunctionName,
                DefaultVersion,
                instance.InstanceId,
                reason: "NewInstance",
                functionType: FunctionType.Orchestrator,
                isReplay: false);

            return instance.InstanceId;
        }

        /// <inheritdoc />
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate", Justification = "This method does not work with the .NET Framework event model.")]
        public override async Task RaiseEventAsync(string instanceId, string eventName, object eventData)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                throw new ArgumentNullException(nameof(eventName));
            }

            OrchestrationState state = await this.GetOrchestrationInstanceAsync(instanceId);

            if (state.OrchestrationStatus == OrchestrationStatus.Running ||
                state.OrchestrationStatus == OrchestrationStatus.Pending ||
                state.OrchestrationStatus == OrchestrationStatus.ContinuedAsNew)
            {
                await this.client.RaiseEventAsync(state.OrchestrationInstance, eventName, eventData);

                this.traceHelper.FunctionScheduled(
                    this.hubName,
                    state.Name,
                    state.Version,
                    state.OrchestrationInstance.InstanceId,
                    reason: "RaiseEvent:" + eventName,
                    functionType: FunctionType.Orchestrator,
                    isReplay: false);
            }
        }

        /// <inheritdoc />
        public override async Task TerminateAsync(string instanceId, string reason)
        {
            OrchestrationState state = await this.GetOrchestrationInstanceAsync(instanceId);
            if (state.OrchestrationStatus == OrchestrationStatus.Running ||
                state.OrchestrationStatus == OrchestrationStatus.Pending ||
                state.OrchestrationStatus == OrchestrationStatus.ContinuedAsNew)
            {
                await this.client.TerminateInstanceAsync(state.OrchestrationInstance, reason);

                this.traceHelper.FunctionTerminated(this.hubName, state.Name, state.Version, instanceId, reason);
            }
        }

        /// <inheritdoc />
        public override async Task<DurableOrchestrationStatus> GetStatusAsync(string instanceId, bool showHistory = false, bool showHistoryInputOutput = false)
        {
            OrchestrationState state = await this.client.GetOrchestrationStateAsync(instanceId);
            if (state == null)
            {
                return null;
            }

            return await this.GetDurableOrchestrationStatusAsync(state, showHistory, showHistoryInputOutput);
        }

        private static JToken ParseToJToken(string value)
        {
            if (value == null)
            {
                return null;
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
            if (state == null || state.OrchestrationInstance == null)
            {
                throw new ArgumentException($"No instance with ID '{instanceId}' was found.", nameof(instanceId));
            }

            return state;
        }

        private async Task<DurableOrchestrationStatus> GetDurableOrchestrationStatusAsync(OrchestrationState orchestrationState, bool showHistory, bool showHistoryInputOutput)
        {
            JArray historyArray = null;
            if (showHistory)
            {
                var history = await this.client.GetOrchestrationHistoryAsync(orchestrationState.OrchestrationInstance);
                if (!string.IsNullOrEmpty(history))
                {
                    historyArray = JArray.Parse(history);

                    var eventMapper = new Dictionary<string, EventIndexDateMapping>();
                    var indexList = new List<int>();

                    for (var i = 0; i < historyArray.Count; i++)
                    {
                        var historyItem = historyArray[i];
                        if (Enum.TryParse(historyItem["EventType"].Value<string>(), out EventType eventType))
                        {
                            historyItem["EventType"] = eventType.ToString();
                            switch (eventType)
                            {
                                case EventType.TaskScheduled:
                                    TrackNameAndScheduledTime(historyItem, eventType, i, ref eventMapper);
                                    RemoveProperties(ref historyItem, "Version", !showHistoryInputOutput);
                                    break;
                                case EventType.TaskCompleted:
                                case EventType.TaskFailed:
                                    AddScheduledEventDataAndAggreagate(eventMapper, "TaskScheduled", historyItem, ref indexList, showHistoryInputOutput);
                                    RemoveProperties(ref historyItem, "TaskScheduledId", hideResult: !showHistoryInputOutput && eventType == EventType.TaskCompleted);
                                    break;
                                case EventType.SubOrchestrationInstanceCreated:
                                    TrackNameAndScheduledTime(historyItem, eventType, i, ref eventMapper);
                                    RemoveProperties(ref historyItem, "Version", !showHistoryInputOutput);
                                    break;
                                case EventType.SubOrchestrationInstanceCompleted:
                                case EventType.SubOrchestrationInstanceFailed:
                                    AddScheduledEventDataAndAggreagate(eventMapper, "SubOrchestrationInstanceCreated", historyItem, ref indexList, showHistoryInputOutput);
                                    RemoveProperties(ref historyItem, "TaskScheduledId", hideResult: !showHistoryInputOutput && eventType == EventType.SubOrchestrationInstanceCompleted);
                                    break;
                                case EventType.ExecutionStarted:
                                    RemoveProperties(ref historyItem, new List<string> { "OrchestrationInstance", "ParentInstance", "Version", "Tags" }, !showHistoryInputOutput);
                                    break;
                                case EventType.ExecutionCompleted:
                                    if (Enum.TryParse(historyItem["OrchestrationStatus"].Value<string>(), out OrchestrationStatus orchestrationStatus))
                                    {
                                        historyItem["OrchestrationStatus"] = orchestrationStatus.ToString();
                                    }

                                    RemoveProperties(ref historyItem, string.Empty, hideResult: !showHistoryInputOutput);
                                    break;
                                case EventType.ExecutionTerminated:
                                    RemoveProperties(ref historyItem, string.Empty, !showHistoryInputOutput);
                                    break;
                                case EventType.TimerFired:
                                    RemoveProperties(ref historyItem, "TimerId");
                                    break;
                                case EventType.EventRaised:
                                    RemoveProperties(ref historyItem, string.Empty, !showHistoryInputOutput);
                                    break;
                            }

                            RemoveProperties(ref historyItem, new List<string> { "EventId", "IsPlayed" });
                        }
                    }

                    var counter = 0;
                    foreach (var indexValue in indexList)
                    {
                        historyArray.RemoveAt(indexValue - counter);
                        counter++;
                    }
                }
            }

            return new DurableOrchestrationStatus
            {
                Name = orchestrationState.Name,
                InstanceId = orchestrationState.OrchestrationInstance.InstanceId,
                CreatedTime = orchestrationState.CreatedTime,
                LastUpdatedTime = orchestrationState.LastUpdatedTime,
                RuntimeStatus = (OrchestrationRuntimeStatus)orchestrationState.OrchestrationStatus,
                Input = ParseToJToken(orchestrationState.Input),
                Output = ParseToJToken(orchestrationState.Output),
                History = historyArray,
            };
        }

        private static void RemoveProperties(ref JToken jsonToken, List<string> propertyNames, bool hideInput = false, bool hideResult = false)
        {
            if (jsonToken == null)
            {
                return;
            }

            if ((hideInput || hideResult) && propertyNames == null)
            {
                propertyNames = new List<string>();
            }

            if (hideInput)
            {
                propertyNames.Add("Input");
            }

            if (hideResult)
            {
                propertyNames.Add("Result");
            }

            foreach (var propertyName in propertyNames)
            {
                jsonToken[propertyName]?.Parent?.Remove();
            }
        }

        private static void RemoveProperties(ref JToken jsonToken, string properyName, bool hideInput = false, bool hideResult = false)
        {
            if (jsonToken == null)
            {
                return;
            }

            if (hideInput)
            {
                jsonToken["Input"]?.Parent?.Remove();
            }

            if (hideResult)
            {
                jsonToken["Result"]?.Parent?.Remove();
            }

            if (!string.IsNullOrEmpty(properyName))
            {
                jsonToken[properyName]?.Parent?.Remove();
            }
        }

        private static void TrackNameAndScheduledTime(JToken historyItem, EventType eventType, int index, ref Dictionary<string, EventIndexDateMapping> eventMapper)
        {
            eventMapper.Add($"{eventType}_{historyItem["EventId"]}", new EventIndexDateMapping { Index = index, Name = historyItem["Name"].ToString(), Input = historyItem["Input"]?.ToString(), Date = (DateTime)historyItem["Timestamp"] });
        }

        private static void AddScheduledEventDataAndAggreagate(IReadOnlyDictionary<string, EventIndexDateMapping> eventMapper, string prefix, JToken historyItem, ref List<int> indexList, bool showHistoryInputOutput)
        {
            var taskScheduledData = eventMapper[$"{prefix}_{historyItem["TaskScheduledId"]}"];
            historyItem["ScheduledTime"] = taskScheduledData.Date;
            historyItem["SchedulerName"] = taskScheduledData.Name;
            if (showHistoryInputOutput)
            {
                historyItem["Input"] = taskScheduledData.Input;
            }

            indexList.Add(taskScheduledData.Index);
        }
    }
}
