// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using DurableTask.Core;
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
        public override async Task<string> StartNewAsync(string orchestratorFunctionName, string instanceId, object input, bool waitUntilOrchestrationStarts = true)
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

            if (waitUntilOrchestrationStarts)
            {
                DurableOrchestrationStatus status = await this.GetStatusAsync(instance.InstanceId);
                Stopwatch stopwatch = Stopwatch.StartNew();
                while ((status == null || status.RuntimeStatus == OrchestrationRuntimeStatus.Pending) && stopwatch.Elapsed < TimeSpan.FromSeconds(10))
                {
                    await Task.Delay(200);
                    status = await this.GetStatusAsync(instanceId);
                }
            }

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
        public override async Task<DurableOrchestrationStatus> GetStatusAsync(string instanceId)
        {
            OrchestrationState state = await this.client.GetOrchestrationStateAsync(instanceId);
            if (state == null)
            {
                return null;
            }

            return new DurableOrchestrationStatus
            {
                Name = state.Name,
                InstanceId = state.OrchestrationInstance.InstanceId,
                CreatedTime = state.CreatedTime,
                LastUpdatedTime = state.LastUpdatedTime,
                RuntimeStatus = (OrchestrationRuntimeStatus)state.OrchestrationStatus,
                Input = ParseToJToken(state.Input),
                Output = ParseToJToken(state.Output),
            };
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
    }
}
