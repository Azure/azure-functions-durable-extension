// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Provides functionality available to durable orchestration clients.
    /// </summary>
    public interface IDurableOrchestrationClient
    {
        /// <summary>
        /// Gets the name of the task hub configured on this client instance.
        /// </summary>
        /// <value>
        /// The name of the task hub.
        /// </value>
        string TaskHubName { get; }

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
        /// <param name="returnInternalServerErrorOnFailure">Optional parameter that configures the http response code returned. Defaults to <c>false</c>.
        /// If <c>true</c>, the returned http response code will be a 500 when the orchestrator is in a failed state, when <c>false</c> it will
        /// return 200.</param>
        /// <returns>An HTTP 202 response with a Location header and a payload containing instance control URLs.</returns>
        HttpResponseMessage CreateCheckStatusResponse(HttpRequestMessage request, string instanceId, bool returnInternalServerErrorOnFailure = false);

        /// <summary>
        /// Creates an HTTP response that is useful for checking the status of the specified instance.
        /// </summary>
        /// <remarks>
        /// The payload of the returned <see cref="IActionResult"/> contains HTTP API URLs that can
        /// be used to query the status of the orchestration, raise events to the orchestration, or
        /// terminate the orchestration.
        /// </remarks>
        /// <param name="request">The HTTP request that triggered the current orchestration instance.</param>
        /// <param name="instanceId">The ID of the orchestration instance to check.</param>
        /// <param name="returnInternalServerErrorOnFailure">Optional parameter that configures the http response code returned. Defaults to <c>false</c>.
        /// If <c>true</c>, the returned http response code will be a 500 when the orchestrator is in a failed state, when <c>false</c> it will
        /// return 200.</param>
        /// <returns>An HTTP 202 response with a Location header and a payload containing instance control URLs.</returns>
        IActionResult CreateCheckStatusResponse(HttpRequest request, string instanceId, bool returnInternalServerErrorOnFailure = false);

        /// <summary>
        /// Creates a <see cref="HttpManagementPayload"/> object that contains status, terminate and send external event HTTP endpoints.
        /// </summary>
        /// <param name="instanceId">The ID of the orchestration instance to check.</param>
        /// <returns>Instance of the <see cref="HttpManagementPayload"/> class.</returns>
        HttpManagementPayload CreateHttpManagementPayload(string instanceId);

        /// <summary>
        /// Creates an HTTP response which either contains a payload of management URLs for a non-completed instance
        /// or contains the payload containing the output of the completed orchestration.
        /// </summary>
        /// <remarks>
        /// If the orchestration instance completes within the default 10 second timeout, then the HTTP response payload will
        /// contain the output of the orchestration instance formatted as JSON. However, if the orchestration does not
        /// complete within this timeout, then the HTTP response will be identical to that of the
        /// <see cref="IDurableOrchestrationClient.CreateCheckStatusResponse(HttpRequestMessage, string, bool)"/> API.
        /// </remarks>
        /// <param name="request">The HTTP request that triggered the current function.</param>
        /// <param name="instanceId">The unique ID of the instance to check.</param>
        /// <returns>An HTTP response which may include a 202 and location header or a 200 with the durable function output in the response body.</returns>
        Task<HttpResponseMessage> WaitForCompletionOrCreateCheckStatusResponseAsync(
            HttpRequestMessage request,
            string instanceId);

        /// <summary>
        /// Creates an HTTP response which either contains a payload of management URLs for a non-completed instance
        /// or contains the payload containing the output of the completed orchestration.
        /// </summary>
        /// <remarks>
        /// If the orchestration instance completes within the specified timeout, then the HTTP response payload will
        /// contain the output of the orchestration instance formatted as JSON. However, if the orchestration does not
        /// complete within the specified timeout, then the HTTP response will be identical to that of the
        /// <see cref="IDurableOrchestrationClient.CreateCheckStatusResponse(HttpRequestMessage, string, bool)"/> API.
        /// </remarks>
        /// <param name="request">The HTTP request that triggered the current function.</param>
        /// <param name="instanceId">The unique ID of the instance to check.</param>
        /// <param name="timeout">Total allowed timeout for output from the durable function. The default value is 10 seconds.</param>
        /// <returns>An HTTP response which may include a 202 and location header or a 200 with the durable function output in the response body.</returns>
        Task<HttpResponseMessage> WaitForCompletionOrCreateCheckStatusResponseAsync(
            HttpRequestMessage request,
            string instanceId,
            TimeSpan timeout);

        /// <summary>
        /// Creates an HTTP response which either contains a payload of management URLs for a non-completed instance
        /// or contains the payload containing the output of the completed orchestration.
        /// </summary>
        /// <remarks>
        /// If the orchestration instance completes within the specified timeout, then the HTTP response payload will
        /// contain the output of the orchestration instance formatted as JSON. However, if the orchestration does not
        /// complete within the specified timeout, then the HTTP response will be identical to that of the
        /// <see cref="CreateCheckStatusResponse(HttpRequestMessage, string, bool)"/> API.
        /// </remarks>
        /// <param name="request">The HTTP request that triggered the current function.</param>
        /// <param name="instanceId">The unique ID of the instance to check.</param>
        /// <param name="timeout">Total allowed timeout for output from the durable function. The default value is 10 seconds.</param>
        /// <param name="retryInterval">The timeout between checks for output from the durable function. The default value is 1 second.</param>
        /// <returns>An HTTP response which may include a 202 and location header or a 200 with the durable function output in the response body.</returns>
        Task<HttpResponseMessage> WaitForCompletionOrCreateCheckStatusResponseAsync(
            HttpRequestMessage request,
            string instanceId,
            TimeSpan timeout,
            TimeSpan retryInterval);

        /// <summary>
        /// Creates an HTTP response which either contains a payload of management URLs for a non-completed instance
        /// or contains the payload containing the output of the completed orchestration.
        /// </summary>
        /// <remarks>
        /// If the orchestration instance completes within the default 10 second timeout, then the HTTP response payload will
        /// contain the output of the orchestration instance formatted as JSON. However, if the orchestration does not
        /// complete within this timeout, then the HTTP response will be identical to that of the
        /// <see cref="IDurableOrchestrationClient.CreateCheckStatusResponse(HttpRequest, string, bool)"/> API.
        /// </remarks>
        /// <param name="request">The HTTP request that triggered the current function.</param>
        /// <param name="instanceId">The unique ID of the instance to check.</param>
        /// <returns>An HTTP response which may include a 202 and location header or a 200 with the durable function output in the response body.</returns>
        Task<IActionResult> WaitForCompletionOrCreateCheckStatusResponseAsync(
            HttpRequest request,
            string instanceId);

        /// <summary>
        /// Creates an HTTP response which either contains a payload of management URLs for a non-completed instance
        /// or contains the payload containing the output of the completed orchestration.
        /// </summary>
        /// <remarks>
        /// If the orchestration instance completes within the specified timeout, then the HTTP response payload will
        /// contain the output of the orchestration instance formatted as JSON. However, if the orchestration does not
        /// complete within the specified timeout, then the HTTP response will be identical to that of the
        /// <see cref="IDurableOrchestrationClient.CreateCheckStatusResponse(HttpRequest, string, bool)"/> API.
        /// </remarks>
        /// <param name="request">The HTTP request that triggered the current function.</param>
        /// <param name="instanceId">The unique ID of the instance to check.</param>
        /// <param name="timeout">Total allowed timeout for output from the durable function. The default value is 10 seconds.</param>
        /// <returns>An HTTP response which may include a 202 and location header or a 200 with the durable function output in the response body.</returns>
        Task<IActionResult> WaitForCompletionOrCreateCheckStatusResponseAsync(
            HttpRequest request,
            string instanceId,
            TimeSpan timeout);

        /// <summary>
        /// Creates an HTTP response which either contains a payload of management URLs for a non-completed instance
        /// or contains the payload containing the output of the completed orchestration.
        /// </summary>
        /// <remarks>
        /// If the orchestration instance completes within the specified timeout, then the HTTP response payload will
        /// contain the output of the orchestration instance formatted as JSON. However, if the orchestration does not
        /// complete within the specified timeout, then the HTTP response will be identical to that of the
        /// <see cref="CreateCheckStatusResponse(HttpRequest, string, bool)"/> API.
        /// </remarks>
        /// <param name="request">The HTTP request that triggered the current function.</param>
        /// <param name="instanceId">The unique ID of the instance to check.</param>
        /// <param name="timeout">Total allowed timeout for output from the durable function. The default value is 10 seconds.</param>
        /// <param name="retryInterval">The timeout between checks for output from the durable function. The default value is 1 second.</param>
        /// <returns>An HTTP response which may include a 202 and location header or a 200 with the durable function output in the response body.</returns>
        Task<IActionResult> WaitForCompletionOrCreateCheckStatusResponseAsync(
            HttpRequest request,
            string instanceId,
            TimeSpan timeout,
            TimeSpan retryInterval);

        /// <summary>
        /// Starts a new execution of the specified orchestrator function.
        /// </summary>
        /// <param name="orchestratorFunctionName">The name of the orchestrator function to start.</param>
        /// <returns>A task that completes when the orchestration is started. The task contains the instance id of the started
        /// orchestratation instance.</returns>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        Task<string> StartNewAsync(
            string orchestratorFunctionName);

        /// <summary>
        /// Starts a new execution of the specified orchestrator function.
        /// </summary>
        /// <param name="orchestratorFunctionName">The name of the orchestrator function to start.</param>
        /// <param name="instanceId">The ID to use for the new orchestration instance.</param>
        /// <returns>A task that completes when the orchestration is started. The task contains the instance id of the started
        /// orchestratation instance.</returns>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        Task<string> StartNewAsync(
            string orchestratorFunctionName,
            string instanceId);

        /// <summary>
        /// Starts a new execution of the specified orchestrator function.
        /// </summary>
        /// <param name="orchestratorFunctionName">The name of the orchestrator function to start.</param>
        /// <param name="input">JSON-serializeable input value for the orchestrator function.</param>
        /// <typeparam name="T">The type of the input value for the orchestrator function.</typeparam>
        /// <returns>A task that completes when the orchestration is started. The task contains the instance id of the started
        /// orchestratation instance.</returns>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        Task<string> StartNewAsync<T>(
            string orchestratorFunctionName,
            T input)
            where T : class;

        /// <summary>
        /// Starts a new instance of the specified orchestrator function.
        /// </summary>
        /// <remarks>
        /// If an orchestration instance with the specified ID already exists, the existing instance
        /// will be silently replaced by this new instance.
        /// </remarks>
        /// <param name="orchestratorFunctionName">The name of the orchestrator function to start.</param>
        /// <param name="instanceId">The ID to use for the new orchestration instance.</param>
        /// <param name="input">JSON-serializable input value for the orchestrator function.</param>
        /// <typeparam name="T">The type of the input value for the orchestrator function.</typeparam>
        /// <returns>A task that completes when the orchestration is started. The task contains the instance id of the started
        /// orchestratation instance.</returns>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        Task<string> StartNewAsync<T>(string orchestratorFunctionName, string instanceId, T input);

        /// <summary>
        /// Sends an event notification message to a waiting orchestration instance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In order to handle the event, the target orchestration instance must be waiting for an
        /// event named <paramref name="eventName"/> using the
        /// <see cref="IDurableOrchestrationContext.WaitForExternalEvent(string)"/> API.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentException">The instance id does not corespond to a valid orchestration instance.</exception>
        /// <exception cref="InvalidOperationException">The orchestration instance with the provided instance id is not running.</exception>
        /// <param name="instanceId">The ID of the orchestration instance that will handle the event.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <returns>A task that completes when the event notification message has been enqueued.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate", Justification = "This method does not work with the .NET Framework event model.")]
        Task RaiseEventAsync(
            string instanceId,
            string eventName);

        /// <summary>
        /// Sends an event notification message to a waiting orchestration instance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In order to handle the event, the target orchestration instance must be waiting for an
        /// event named <paramref name="eventName"/> using the
        /// <see cref="IDurableOrchestrationContext.WaitForExternalEvent{T}(string)"/> API.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentException">The instance id does not corespond to a valid orchestration instance.</exception>
        /// <exception cref="InvalidOperationException">The orchestration instance with the provided instance id is not running.</exception>
        /// <param name="instanceId">The ID of the orchestration instance that will handle the event.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="eventData">The JSON-serializeable data associated with the event.</param>
        /// <returns>A task that completes when the event notification message has been enqueued.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate", Justification = "This method does not work with the .NET Framework event model.")]
        Task RaiseEventAsync(string instanceId, string eventName, object eventData);

        /// <summary>
        /// Sends an event notification message to a waiting orchestration instance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In order to handle the event, the target orchestration instance must be waiting for an
        /// event named <paramref name="eventName"/> using the
        /// <see cref="IDurableOrchestrationContext.WaitForExternalEvent{T}(string)"/> API.
        /// </para><para>
        /// If the specified instance is not found or not running, this operation will throw an exception.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentException">The instance id does not corespond to a valid orchestration instance.</exception>
        /// <exception cref="InvalidOperationException">The orchestration instance with the provided instance id is not running.</exception>
        /// <param name="taskHubName">The TaskHubName of the orchestration that will handle the event.</param>
        /// <param name="instanceId">The ID of the orchestration instance that will handle the event.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="eventData">The JSON-serializeable data associated with the event.</param>
        /// <param name="connectionName">The name of the connection string associated with <paramref name="taskHubName"/>.</param>
        /// <returns>A task that completes when the event notification message has been enqueued.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate", Justification = "This method does not work with the .NET Framework event model.")]
        Task RaiseEventAsync(string taskHubName, string instanceId, string eventName, object eventData, string connectionName = null);

        /// <summary>
        /// Terminates a running orchestration instance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A terminated instance will eventually transition into the <see cref="OrchestrationRuntimeStatus.Terminated"/> state.
        /// However, this transition will not happen immediately. Rather, the terminate operation will be queued in the task hub
        /// along with other operations for that instance. You can use the <see cref="GetStatusAsync(string, bool, bool, bool)"/>
        /// method to know when a terminated instance has actually reached the Terminated state.
        /// </para>
        /// <para>
        /// Terminating an orchestration instance has no effect on any in-flight activity function executions
        /// or sub-orchestrations that were started by the current orchestration instance.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentException">The instance id does not corespond to a valid orchestration instance.</exception>
        /// <exception cref="InvalidOperationException">The orchestration instance with the provided instance id is not running.</exception>
        /// <param name="instanceId">The ID of the orchestration instance to terminate.</param>
        /// <param name="reason">The reason for terminating the orchestration instance.</param>
        /// <returns>A task that completes when the terminate message is enqueued if necessary.</returns>
        Task TerminateAsync(string instanceId, string reason);

        /// <summary>
        /// Rewinds the specified failed orchestration instance with a reason.
        /// </summary>
        /// <param name="instanceId">The ID of the orchestration instance to rewind.</param>
        /// <param name="reason">The reason for rewinding the orchestration instance.</param>
        /// <returns>A task that completes when the rewind message is enqueued.</returns>
        [Obsolete("This feature is in preview.")]
        Task RewindAsync(string instanceId, string reason);

        /// <summary>
        /// Gets the status of the specified orchestration instance.
        /// </summary>
        /// <param name="instanceId">The ID of the orchestration instance to query.</param>
        /// <returns>Returns a task which completes when the status has been fetched.</returns>
        Task<DurableOrchestrationStatus> GetStatusAsync(string instanceId);

        /// <summary>
        /// Gets the status of the specified orchestration instance.
        /// </summary>
        /// <param name="instanceId">The ID of the orchestration instance to query.</param>
        /// <param name="showHistory">Boolean marker for including execution history in the response.</param>
        /// <returns>Returns a task which completes when the status has been fetched.</returns>
        Task<DurableOrchestrationStatus> GetStatusAsync(string instanceId, bool showHistory);

        /// <summary>
        /// Gets the status of the specified orchestration instance.
        /// </summary>
        /// <param name="instanceId">The ID of the orchestration instance to query.</param>
        /// <param name="showHistory">Boolean marker for including execution history in the response.</param>
        /// <param name="showHistoryOutput">Boolean marker for including input and output in the execution history response.</param>
        /// <param name="showInput">If set, fetch and return the input for the orchestration instance.</param>
        /// <returns>Returns a task which completes when the status has been fetched.</returns>
        Task<DurableOrchestrationStatus> GetStatusAsync(string instanceId, bool showHistory, bool showHistoryOutput, bool showInput);

        /// <summary>
        /// Gets all the status of the orchestration instances.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token that can be used to cancel the status query operation.</param>
        /// <returns>Returns orchestration status for all instances.</returns>
        [Obsolete]
        Task<IList<DurableOrchestrationStatus>> GetStatusAsync(CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Gets the status of all orchestration instances that match the specified conditions.
        /// </summary>
        /// <param name="createdTimeFrom">Return orchestration instances which were created after this DateTime.</param>
        /// <param name="createdTimeTo">Return orchestration instances which were created before this DateTime.</param>
        /// <param name="runtimeStatus">Return orchestration instances which matches the runtimeStatus.</param>
        /// <param name="cancellationToken">Cancellation token that can be used to cancel the status query operation.</param>
        /// <returns>Returns orchestration status for all instances.</returns>
        [Obsolete]
        Task<IList<DurableOrchestrationStatus>> GetStatusAsync(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationRuntimeStatus> runtimeStatus, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Purge the history for a concrete instance.
        /// </summary>
        /// <param name="instanceId">The ID of the orchestration instance to purge.</param>
        /// <returns>Returns an instance of <see cref="PurgeHistoryResult"/>.</returns>
        Task<PurgeHistoryResult> PurgeInstanceHistoryAsync(string instanceId);

        /// <summary>
        /// Purge the orchestration history for instances that match the conditions.
        /// </summary>
        /// <param name="createdTimeFrom">Start creation time for querying instances for purging.</param>
        /// <param name="createdTimeTo">End creation time for querying instances for purging.</param>
        /// <param name="runtimeStatus">List of runtime status for querying instances for purging. Only Completed, Terminated, or Failed will be processed.</param>
        /// <returns>Returns an instance of <see cref="PurgeHistoryResult"/>.</returns>
        Task<PurgeHistoryResult> PurgeInstanceHistoryAsync(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationStatus> runtimeStatus);

        /// <summary>
        /// Gets the status of all orchestration instances with paging that match the specified conditions.
        /// </summary>
        /// <param name="condition">Return orchestration instances that match the specified conditions.</param>
        /// <param name="cancellationToken">Cancellation token that can be used to cancel the status query operation.</param>
        /// <returns>Returns each page of orchestration status for all instances and continuation token of next page.</returns>
        [Obsolete]
        Task<OrchestrationStatusQueryResult> GetStatusAsync(OrchestrationStatusQueryCondition condition, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the status of all orchestration instances with paging that match the specified conditions.
        /// </summary>
        /// <param name="condition">Return orchestration instances that match the specified conditions.</param>
        /// <param name="cancellationToken">Cancellation token that can be used to cancel the status query operation.</param>
        /// <returns>Returns each page of orchestration status for all instances and continuation token of next page.</returns>
        Task<OrchestrationStatusQueryResult> ListInstancesAsync(OrchestrationStatusQueryCondition condition, CancellationToken cancellationToken);
    }
}
