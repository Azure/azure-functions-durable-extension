// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
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
    public class DurableOrchestrationClient
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

        /// <summary>
        /// Creates an HTTP response for checking the status of the specified instance.
        /// </summary>
        /// <param name="request">The HTTP request that triggered the current function.</param>
        /// <param name="instanceId">The unique ID of the instance to check.</param>
        /// <returns>An HTTP response which may include a 202 and location header.</returns>
        public HttpResponseMessage CreateCheckStatusResponse(HttpRequestMessage request, string instanceId)
        {
            return this.config.CreateCheckStatusResponse(request, instanceId, this.attribute);
        }

        /// <summary>
        /// Creates an HTTP response for checking the status of the specified instance supporting synchronous response as well.
        /// </summary>
        /// <param name="request">The HTTP request that triggered the current function.</param>
        /// <param name="instanceId">The unique ID of the instance to check.</param>
        /// <param name="timeout">Total allowed timeout for output from the durable function. The default value is 10 seconds.</param>
        /// <param name="retryInterval">The timeout between checks for output from the durable function. The default value is 1 second.</param>
        /// <returns>An HTTP response which may include a 202 and location header or a 200 with the durable function output in the response body.</returns>
        public async Task<HttpResponseMessage> WaitForCompletionOrCreateCheckStatusResponseAsync(HttpRequestMessage request, string instanceId, TimeSpan? timeout, TimeSpan? retryInterval)
        {
            return await this.config.WaitForCompletionOrCreateCheckStatusResponseAsync(request, instanceId, this.attribute, timeout ?? TimeSpan.FromSeconds(10), retryInterval ?? TimeSpan.FromSeconds(1));
        }

        /// <summary>
        /// Starts a new execution of the specified orchestrator function.
        /// </summary>
        /// <param name="orchestratorFunctionName">The name of the orchestrator function to start.</param>
        /// <param name="input">JSON-serializeable input value for the orchestrator function.</param>
        /// <returns>A task that completes when the start message is enqueued.</returns>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        public Task<string> StartNewAsync(string orchestratorFunctionName, object input)
        {
            return this.StartNewAsync(orchestratorFunctionName, string.Empty, input);
        }

        /// <summary>
        /// Starts a new execution of the specified orchestrator function.
        /// </summary>
        /// <param name="orchestratorFunctionName">The name of the orchestrator function to start.</param>
        /// <param name="instanceId">A unique ID to use for the new orchestration instance.</param>
        /// <param name="input">JSON-serializeable input value for the orchestrator function.</param>
        /// <returns>A task that completes when the start message is enqueued.</returns>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        public async Task<string> StartNewAsync(string orchestratorFunctionName, string instanceId, object input)
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

        /// <summary>
        /// Sends an event notification message to a running orchestration instance.
        /// </summary>
        /// <param name="instanceId">The ID of the orchestration instance that will handle the event.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="eventData">The JSON-serializeable data associated with the event.</param>
        /// <returns>A task that completes when the event notification message has been enqueued.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate", Justification = "An event is not appropriate in this case")]
        public async Task RaiseEventAsync(string instanceId, string eventName, object eventData)
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

        /// <summary>
        /// Terminates a running orchestration instance.
        /// </summary>
        /// <param name="instanceId">The ID of the orchestration instance to terminate.</param>
        /// <param name="reason">The reason for terminating the orchestration instance.</param>
        /// <returns>A task that completes when the terminate message is enqueued.</returns>
        public async Task TerminateAsync(string instanceId, string reason)
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

        /// <summary>
        /// Gets the status of the specified orchestration instance.
        /// </summary>
        /// <param name="instanceId">The ID of the orchestration instance to query.</param>
        /// <returns>Returns a task which completes when the status has been fetched.</returns>
        public virtual async Task<DurableOrchestrationStatus> GetStatusAsync(string instanceId)
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
