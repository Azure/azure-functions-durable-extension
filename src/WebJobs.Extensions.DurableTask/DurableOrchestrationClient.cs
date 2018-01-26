// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
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
                        if (Enum.TryParse(historyArray[i]["EventType"].Value<string>(), out EventType eventType))
                        {
                            historyArray[i]["EventType"] = eventType.ToString();
                            switch (eventType)
                            {
                                case EventType.TaskScheduled:
                                    eventMapper.Add($"{eventType}_{historyArray[i]["EventId"]}", new EventIndexDateMapping { Index = i,  Date = DateTime.Parse(historyArray[i]["Timestamp"].ToString()) });
                                    break;
                                case EventType.TaskCompleted:
                                case EventType.TaskFailed:
                                    var taskScheduledData = eventMapper[$"TaskScheduled_{historyArray[i]["TaskScheduledId"]}"];
                                    historyArray[i]["StartTime"] = taskScheduledData.Date;
                                    indexList.Add(taskScheduledData.Index);
                                    break;
                                case EventType.SubOrchestrationInstanceCreated:
                                    eventMapper.Add($"{eventType}_{historyArray[i]["EventId"]}", new EventIndexDateMapping { Index = i, Date = DateTime.Parse(historyArray[i]["Timestamp"].ToString()) });
                                    break;
                                case EventType.SubOrchestrationInstanceCompleted:
                                case EventType.SubOrchestrationInstanceFailed:
                                    var subOrchestrationCreatedData = eventMapper[$"SubOrchestrationInstanceCreated_{historyArray[i]["TaskScheduledId"]}"];
                                    historyArray[i]["StartTime"] = subOrchestrationCreatedData.Date;
                                    indexList.Add(subOrchestrationCreatedData.Index);
                                    break;
                            }
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

            this.RemoveProperties(ref historyArray, showHistoryInputOutput);

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

        // TBD - duplication to be removed
        private void RemoveProperties(ref JArray jsonArray, bool showHistoryInputOutput)
        {
            if (jsonArray == null)
            {
                return;
            }

            if (!showHistoryInputOutput)
            {
                jsonArray.Descendants()
                    .OfType<JProperty>()
                    .Where(attr =>
                        attr.Name.StartsWith("Input") ||
                        attr.Name.StartsWith("Result") ||
                        attr.Name.StartsWith("Version") ||
                        attr.Name.StartsWith("TaskScheduledId") ||
                        attr.Name.StartsWith("IsPlayed") ||
                        attr.Name.StartsWith("OrchestrationInstance") ||
                        attr.Name.StartsWith("ParentInstance"))
                    .ToList()
                    .ForEach(attr => attr.Remove());
            }
            else
            {
                jsonArray.Descendants()
                    .OfType<JProperty>()
                    .Where(attr =>
                        attr.Name.StartsWith("Version") ||
                        attr.Name.StartsWith("TaskScheduledId") ||
                        attr.Name.StartsWith("IsPlayed") ||
                        attr.Name.StartsWith("OrchestrationInstance") ||
                        attr.Name.StartsWith("ParentInstance"))
                    .ToList()
                    .ForEach(attr => attr.Remove());
            }
        }
    }
}
