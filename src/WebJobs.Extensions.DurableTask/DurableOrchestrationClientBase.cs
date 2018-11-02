// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Abstract base class for <see cref="DurableOrchestrationClient"/>.
    /// </summary>
    public abstract class DurableOrchestrationClientBase
    {
        /// <summary>
        /// Gets the name of the task hub configured on this client instance.
        /// </summary>
        /// <value>
        /// The name of the task hub.
        /// </value>
        public abstract string TaskHubName { get;  }

        /// <summary>
        /// Creates an HTTP response that is useful for checking the status of the specified instance.
        /// </summary>
        /// <remarks>
        /// The payload of the returned <see cref="HttpResponseMessage"/> contains HTTP API URLs that can
        /// be used to query the status of the orchestration, raise events to the orchestration, or
        /// terminate the orchestration.
        /// </remarks>
        /// <param name="request">The HTTP request that triggered the current orchestration instance.</param>
        /// <param name="instanceId">The ID of the orchestration instance to check.</param>
        /// <returns>An HTTP 202 response with a Location header and a payload containing instance control URLs.</returns>
        public abstract HttpResponseMessage CreateCheckStatusResponse(HttpRequestMessage request, string instanceId);

        /// <summary>
        /// Creates a <see cref="HttpManagementPayload"/> object that contains status, terminate and send external event HTTP endpoints.
        /// </summary>
        /// <param name="instanceId">The ID of the orchestration instance to check.</param>
        /// <returns>Instance of the <see cref="HttpManagementPayload"/> class.</returns>
        public abstract HttpManagementPayload CreateHttpManagementPayload(string instanceId);

        /// <summary>
        /// Creates an HTTP response which either contains a payload of management URLs for a non-completed instance
        /// or contains the payload containing the output of the completed orchestration.
        /// </summary>
        /// <remarks>
        /// If the orchestration instance completes within the default 10 second timeout, then the HTTP response payload will
        /// contain the output of the orchestration instance formatted as JSON. However, if the orchestration does not
        /// complete within this timeout, then the HTTP response will be identical to that of the
        /// <see cref="CreateCheckStatusResponse"/> API.
        /// </remarks>
        /// <param name="request">The HTTP request that triggered the current function.</param>
        /// <param name="instanceId">The unique ID of the instance to check.</param>
        /// <returns>An HTTP response which may include a 202 and location header or a 200 with the durable function output in the response body.</returns>
        public virtual Task<HttpResponseMessage> WaitForCompletionOrCreateCheckStatusResponseAsync(
            HttpRequestMessage request,
            string instanceId)
        {
            return this.WaitForCompletionOrCreateCheckStatusResponseAsync(
                request,
                instanceId,
                timeout: TimeSpan.FromSeconds(10));
        }

        /// <summary>
        /// Creates an HTTP response which either contains a payload of management URLs for a non-completed instance
        /// or contains the payload containing the output of the completed orchestration.
        /// </summary>
        /// <remarks>
        /// If the orchestration instance completes within the specified timeout, then the HTTP response payload will
        /// contain the output of the orchestration instance formatted as JSON. However, if the orchestration does not
        /// complete within the specified timeout, then the HTTP response will be identical to that of the
        /// <see cref="CreateCheckStatusResponse"/> API.
        /// </remarks>
        /// <param name="request">The HTTP request that triggered the current function.</param>
        /// <param name="instanceId">The unique ID of the instance to check.</param>
        /// <param name="timeout">Total allowed timeout for output from the durable function. The default value is 10 seconds.</param>
        /// <returns>An HTTP response which may include a 202 and location header or a 200 with the durable function output in the response body.</returns>
        public virtual Task<HttpResponseMessage> WaitForCompletionOrCreateCheckStatusResponseAsync(
            HttpRequestMessage request,
            string instanceId,
            TimeSpan timeout)
        {
            return this.WaitForCompletionOrCreateCheckStatusResponseAsync(
                request,
                instanceId,
                timeout,
                retryInterval: TimeSpan.FromSeconds(1));
        }

        /// <summary>
        /// Creates an HTTP response which either contains a payload of management URLs for a non-completed instance
        /// or contains the payload containing the output of the completed orchestration.
        /// </summary>
        /// <remarks>
        /// If the orchestration instance completes within the specified timeout, then the HTTP response payload will
        /// contain the output of the orchestration instance formatted as JSON. However, if the orchestration does not
        /// complete within the specified timeout, then the HTTP response will be identical to that of the
        /// <see cref="CreateCheckStatusResponse"/> API.
        /// </remarks>
        /// <param name="request">The HTTP request that triggered the current function.</param>
        /// <param name="instanceId">The unique ID of the instance to check.</param>
        /// <param name="timeout">Total allowed timeout for output from the durable function. The default value is 10 seconds.</param>
        /// <param name="retryInterval">The timeout between checks for output from the durable function. The default value is 1 second.</param>
        /// <returns>An HTTP response which may include a 202 and location header or a 200 with the durable function output in the response body.</returns>
        public abstract Task<HttpResponseMessage> WaitForCompletionOrCreateCheckStatusResponseAsync(
            HttpRequestMessage request,
            string instanceId,
            TimeSpan timeout,
            TimeSpan retryInterval);

        /// <summary>
        /// Starts a new execution of the specified orchestrator function.
        /// </summary>
        /// <param name="orchestratorFunctionName">The name of the orchestrator function to start.</param>
        /// <param name="input">JSON-serializeable input value for the orchestrator function.</param>
        /// <returns>A task that completes when the orchestration is started.</returns>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        public virtual Task<string> StartNewAsync(string orchestratorFunctionName, object input)
        {
            return this.StartNewAsync(orchestratorFunctionName, string.Empty, input);
        }

        /// <summary>
        /// Starts a new instance of the specified orchestrator function.
        /// </summary>
        /// <remarks>
        /// If an orchestration instance with the specified ID already exists, the existing instance
        /// will be silently replaced by this new instance.
        /// </remarks>
        /// <param name="orchestratorFunctionName">The name of the orchestrator function to start.</param>
        /// <param name="instanceId">The ID to use for the new orchestration instance.</param>
        /// <param name="input">JSON-serializeable input value for the orchestrator function.</param>
        /// <returns>A task that completes when the orchestration is started.</returns>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        public abstract Task<string> StartNewAsync(string orchestratorFunctionName, string instanceId, object input);

        /// <summary>
        /// Sends an event notification message to a waiting orchestration instance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In order to handle the event, the target orchestration instance must be waiting for an
        /// event named <paramref name="eventName"/> using the
        /// <see cref="DurableOrchestrationContext.WaitForExternalEvent{T}(string)"/> API.
        /// </para><para>
        /// If the specified instance is not found or not running, this operation will have no effect.
        /// </para>
        /// </remarks>
        /// <param name="instanceId">The ID of the orchestration instance that will handle the event.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="eventData">The JSON-serializeable data associated with the event.</param>
        /// <returns>A task that completes when the event notification message has been enqueued.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate", Justification = "This method does not work with the .NET Framework event model.")]
        public abstract Task RaiseEventAsync(string instanceId, string eventName, object eventData);

        /// <summary>
        /// Sends an event notification message to a waiting orchestration instance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In order to handle the event, the target orchestration instance must be waiting for an
        /// event named <paramref name="eventName"/> using the
        /// <see cref="DurableOrchestrationContext.WaitForExternalEvent{T}(string)"/> API.
        /// </para><para>
        /// If the specified instance is not found or not running, this operation will have no effect.
        /// </para>
        /// </remarks>
        /// <param name="taskHubName">The TaskHubName of the orchestration that will handle the event.</param>
        /// <param name="instanceId">The ID of the orchestration instance that will handle the event.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="eventData">The JSON-serializeable data associated with the event.</param>
        /// <param name="connectionName">The name of the connection string associated with <paramref name="taskHubName"/>.</param>
        /// <returns>A task that completes when the event notification message has been enqueued.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate", Justification = "This method does not work with the .NET Framework event model.")]
        public abstract Task RaiseEventAsync(string taskHubName, string instanceId, string eventName, object eventData, string connectionName = null);

        /// <summary>
        /// Terminates a running orchestration instance.
        /// </summary>
        /// <remarks>
        /// Terminating an orchestration instance has no effect on any in-flight activity function executions
        /// or sub-orchestrations that were started by the current orchestration instance.
        /// </remarks>
        /// <param name="instanceId">The ID of the orchestration instance to terminate.</param>
        /// <param name="reason">The reason for terminating the orchestration instance.</param>
        /// <returns>A task that completes when the terminate message is enqueued.</returns>
        public abstract Task TerminateAsync(string instanceId, string reason);

        /// <summary>
        /// Rewinds the specified failed orchestration instance with a reason.
        /// </summary>
        /// <param name="instanceId">The ID of the orchestration instance to rewind.</param>
        /// <param name="reason">The reason for rewinding the orchestration instance.</param>
        /// <returns>A task that completes when the rewind message is enqueued.</returns>
        public abstract Task RewindAsync(string instanceId, string reason);

        /// <summary>
        /// Gets the status of the specified orchestration instance.
        /// </summary>
        /// <param name="instanceId">The ID of the orchestration instance to query.</param>
        /// <returns>Returns a task which completes when the status has been fetched.</returns>
        public virtual Task<DurableOrchestrationStatus> GetStatusAsync(string instanceId)
        {
            return this.GetStatusAsync(instanceId, showHistory: false);
        }

        /// <summary>
        /// Gets the status of the specified orchestration instance.
        /// </summary>
        /// <param name="instanceId">The ID of the orchestration instance to query.</param>
        /// <param name="showHistory">Boolean marker for including execution history in the response.</param>
        /// <returns>Returns a task which completes when the status has been fetched.</returns>
        public virtual Task<DurableOrchestrationStatus> GetStatusAsync(string instanceId, bool showHistory)
        {
            return this.GetStatusAsync(instanceId, showHistory, showHistoryOutput: false, showInput: true);
        }

        /// <summary>
        /// Gets the status of the specified orchestration instance.
        /// </summary>
        /// <param name="instanceId">The ID of the orchestration instance to query.</param>
        /// <param name="showHistory">Boolean marker for including execution history in the response.</param>
        /// <param name="showHistoryOutput">Boolean marker for including input and output in the execution history response.</param>
        /// <param name="showInput">If set, fetch and return the input for the orchestration instance.</param>
        /// <returns>Returns a task which completes when the status has been fetched.</returns>
        public abstract Task<DurableOrchestrationStatus> GetStatusAsync(string instanceId, bool showHistory, bool showHistoryOutput, bool showInput = true);

        /// <summary>
        /// Gets all the status of the orchestration instances.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token that can be used to cancel the status query operation.</param>
        /// <returns>Returns orchestration status for all instances.</returns>
        public abstract Task<IList<DurableOrchestrationStatus>> GetStatusAsync(CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Gets the status of all orchestration instances that match the specified conditions.
        /// </summary>
        /// <param name="createdTimeFrom">Return orchestration instances which were created after this DateTime.</param>
        /// <param name="createdTimeTo">Return orchestration instances which were created before this DateTime.</param>
        /// <param name="runtimeStatus">Return orchestration instances which matches the runtimeStatus.</param>
        /// <param name="cancellationToken">Cancellation token that can be used to cancel the status query operation.</param>
        /// <returns>Returns orchestration status for all instances.</returns>
        public abstract Task<IList<DurableOrchestrationStatus>> GetStatusAsync(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationRuntimeStatus> runtimeStatus, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Gets the status of all orchestration instances with paging that match the specified conditions.
        /// </summary>
        /// <remarks>
        /// This is limited to <see cref="HttpApiHandler"/> and it will not be published to external clients.
        /// </remarks>
        /// <param name="createdTimeFrom">Return orchestration instances which were created after this DateTime.</param>
        /// <param name="createdTimeTo">Return orchestration instances which were created before this DateTime.</param>
        /// <param name="runtimeStatus">Return orchestration instances which matches the runtimeStatus.</param>
        /// <param name="pageSize">Number of records per one request.</param>
        /// <param name="continuationToken">ContinuationToken of the pager.</param>
        /// <param name="cancellationToken">Cancellation token that can be used to cancel the status query operation.</param>
        /// <returns>Returns each page of orchestration status for all instances and continuation token of next page.</returns>
        internal abstract Task<OrchestrationStatusQueryResult> GetStatusAsync(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationRuntimeStatus> runtimeStatus, int pageSize, string continuationToken, CancellationToken cancellationToken = default(CancellationToken));
    }
}
