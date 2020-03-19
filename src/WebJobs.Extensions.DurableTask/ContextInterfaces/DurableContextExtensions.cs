// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
#if !FUNCTIONS_V1
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
#endif

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Defines convenient overloads for calling the context methods, for all the contexts.
    /// </summary>
    public static class DurableContextExtensions
    {
        /// <summary>
        /// Schedules an activity function named <paramref name="functionName"/> for execution.
        /// </summary>
        /// <param name="context">The context object.</param>
        /// <param name="functionName">The name of the activity function to call.</param>
        /// <param name="input">The JSON-serializeable input to pass to the activity function.</param>
        /// <returns>A durable task that completes when the called function completes or fails.</returns>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The current thread is different than the thread which started the orchestrator execution.
        /// </exception>
        /// <exception cref="FunctionFailedException">
        /// The activity function failed with an unhandled exception.
        /// </exception>
        public static Task CallActivityAsync(this IDurableOrchestrationContext context, string functionName, object input)
        {
            return context.CallActivityAsync<object>(functionName, input);
        }

        /// <summary>
        /// Schedules an activity function named <paramref name="functionName"/> for execution with retry options.
        /// </summary>
        /// <param name="context">The context object.</param>
        /// <param name="functionName">The name of the activity function to call.</param>
        /// <param name="retryOptions">The retry option for the activity function.</param>
        /// <param name="input">The JSON-serializeable input to pass to the activity function.</param>
        /// <returns>A durable task that completes when the called activity function completes or fails.</returns>
        /// <exception cref="ArgumentNullException">
        /// The retry option object is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The current thread is different than the thread which started the orchestrator execution.
        /// </exception>
        /// <exception cref="FunctionFailedException">
        /// The activity function failed with an unhandled exception.
        /// </exception>
        public static Task CallActivityWithRetryAsync(this IDurableOrchestrationContext context, string functionName, RetryOptions retryOptions, object input)
        {
            return context.CallActivityWithRetryAsync<object>(functionName, retryOptions, input);
        }

        /// <summary>
        /// Schedules an orchestrator function named <paramref name="functionName"/> for execution.
        /// </summary>
        /// <param name="context">The context object.</param>
        /// <param name="functionName">The name of the orchestrator function to call.</param>
        /// <param name="input">The JSON-serializeable input to pass to the orchestrator function.</param>
        /// <returns>A durable task that completes when the called orchestrator function completes or fails.</returns>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The current thread is different than the thread which started the orchestrator execution.
        /// </exception>
        /// <exception cref="FunctionFailedException">
        /// The sub-orchestrator function failed with an unhandled exception.
        /// </exception>
        public static Task CallSubOrchestratorAsync(this IDurableOrchestrationContext context, string functionName, object input)
        {
            return context.CallSubOrchestratorAsync<object>(functionName, input);
        }

        /// <summary>
        /// Schedules an orchestrator function named <paramref name="functionName"/> for execution.
        /// </summary>
        /// <param name="context">The context object.</param>
        /// <param name="functionName">The name of the orchestrator function to call.</param>
        /// <param name="instanceId">A unique ID to use for the sub-orchestration instance.</param>
        /// <param name="input">The JSON-serializeable input to pass to the orchestrator function.</param>
        /// <returns>A durable task that completes when the called orchestrator function completes or fails.</returns>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The current thread is different than the thread which started the orchestrator execution.
        /// </exception>
        /// <exception cref="FunctionFailedException">
        /// The activity function failed with an unhandled exception.
        /// </exception>
        public static Task CallSubOrchestratorAsync(this IDurableOrchestrationContext context, string functionName, string instanceId, object input)
        {
            return context.CallSubOrchestratorAsync<object>(functionName, instanceId, input);
        }

        /// <summary>
        /// Schedules an orchestration function named <paramref name="functionName"/> for execution.
        /// </summary>
        /// <typeparam name="TResult">The return type of the scheduled orchestrator function.</typeparam>
        /// <param name="context">The context object.</param>
        /// <param name="functionName">The name of the orchestrator function to call.</param>
        /// <param name="input">The JSON-serializeable input to pass to the orchestrator function.</param>
        /// <returns>A durable task that completes when the called orchestrator function completes or fails.</returns>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The current thread is different than the thread which started the orchestrator execution.
        /// </exception>
        /// <exception cref="FunctionFailedException">
        /// The activity function failed with an unhandled exception.
        /// </exception>
        public static Task<TResult> CallSubOrchestratorAsync<TResult>(this IDurableOrchestrationContext context, string functionName, object input)
        {
            return context.CallSubOrchestratorAsync<TResult>(functionName, null, input);
        }

        /// <summary>
        /// Schedules an orchestrator function named <paramref name="functionName"/> for execution with retry options.
        /// </summary>
        /// <param name="context">The context object.</param>
        /// <param name="functionName">The name of the orchestrator function to call.</param>
        /// <param name="retryOptions">The retry option for the orchestrator function.</param>
        /// <param name="input">The JSON-serializeable input to pass to the orchestrator function.</param>
        /// <returns>A durable task that completes when the called orchestrator function completes or fails.</returns>
        /// <exception cref="ArgumentNullException">
        /// The retry option object is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The current thread is different than the thread which started the orchestrator execution.
        /// </exception>
        /// <exception cref="FunctionFailedException">
        /// The activity function failed with an unhandled exception.
        /// </exception>
        public static Task CallSubOrchestratorWithRetryAsync(this IDurableOrchestrationContext context, string functionName, RetryOptions retryOptions, object input)
        {
            return context.CallSubOrchestratorWithRetryAsync<object>(functionName, retryOptions, null, input);
        }

        /// <summary>
        /// Schedules an orchestrator function named <paramref name="functionName"/> for execution with retry options.
        /// </summary>
        /// <param name="context">The context object.</param>
        /// <param name="functionName">The name of the orchestrator function to call.</param>
        /// <param name="retryOptions">The retry option for the orchestrator function.</param>
        /// <param name="instanceId">A unique ID to use for the sub-orchestration instance.</param>
        /// <param name="input">The JSON-serializeable input to pass to the orchestrator function.</param>
        /// <returns>A durable task that completes when the called orchestrator function completes or fails.</returns>
        /// <exception cref="ArgumentNullException">
        /// The retry option object is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The current thread is different than the thread which started the orchestrator execution.
        /// </exception>
        /// <exception cref="FunctionFailedException">
        /// The activity function failed with an unhandled exception.
        /// </exception>
        public static Task CallSubOrchestratorWithRetryAsync(this IDurableOrchestrationContext context, string functionName, RetryOptions retryOptions, string instanceId, object input)
        {
            return context.CallSubOrchestratorWithRetryAsync<object>(functionName, retryOptions, instanceId, input);
        }

        /// <summary>
        /// Schedules an orchestrator function named <paramref name="functionName"/> for execution with retry options.
        /// </summary>
        /// <typeparam name="TResult">The return type of the scheduled orchestrator function.</typeparam>
        /// <param name="context">The context object.</param>
        /// <param name="functionName">The name of the orchestrator function to call.</param>
        /// <param name="retryOptions">The retry option for the orchestrator function.</param>
        /// <param name="input">The JSON-serializeable input to pass to the orchestrator function.</param>
        /// <returns>A durable task that completes when the called orchestrator function completes or fails.</returns>
        /// <exception cref="ArgumentNullException">
        /// The retry option object is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The current thread is different than the thread which started the orchestrator execution.
        /// </exception>
        /// <exception cref="FunctionFailedException">
        /// The activity function failed with an unhandled exception.
        /// </exception>
        public static Task<TResult> CallSubOrchestratorWithRetryAsync<TResult>(this IDurableOrchestrationContext context, string functionName, RetryOptions retryOptions, object input)
        {
            return context.CallSubOrchestratorWithRetryAsync<TResult>(functionName, retryOptions, null, input);
        }

        /// <summary>
        /// Creates a durable timer that expires at a specified time.
        /// </summary>
        /// <remarks>
        /// All durable timers created using this method must either expire or be cancelled
        /// using the <paramref name="cancelToken"/> before the orchestrator function completes.
        /// Otherwise the underlying framework will keep the instance alive until the timer expires.
        /// </remarks>
        /// <param name="context">The context object.</param>
        /// <param name="fireAt">The time at which the timer should expire.</param>
        /// <param name="cancelToken">The <c>CancellationToken</c> to use for cancelling the timer.</param>
        /// <returns>A durable task that completes when the durable timer expires.</returns>
        public static Task CreateTimer(this IDurableOrchestrationContext context, DateTime fireAt, CancellationToken cancelToken)
        {
            return context.CreateTimer<object>(fireAt, null, cancelToken);
        }

        /// <summary>
        /// Waits asynchronously for an event to be raised with name <paramref name="name"/>.
        /// </summary>
        /// <remarks>
        /// External clients can raise events to a waiting orchestration instance using
        /// <see cref="IDurableOrchestrationClient.RaiseEventAsync(string, string, object)"/> with the object parameter set to <c>null</c>.
        /// </remarks>
        /// <param name="context">The context object.</param>
        /// <param name="name">The name of the event to wait for.</param>
        /// <returns>A durable task that completes when the external event is received.</returns>
        public static Task WaitForExternalEvent(this IDurableOrchestrationContext context, string name)
        {
            return context.WaitForExternalEvent<object>(name);
        }

        /// <summary>
        /// Waits asynchronously for an event to be raised with name <paramref name="name"/>.
        /// </summary>
        /// <remarks>
        /// External clients can raise events to a waiting orchestration instance using
        /// <see cref="IDurableOrchestrationClient.RaiseEventAsync(string, string, object)"/> with the object parameter set to <c>null</c>.
        /// </remarks>
        /// <param name="context">The context object.</param>
        /// <param name="name">The name of the event to wait for.</param>
        /// <param name="timeout">The duration after which to throw a TimeoutException.</param>
        /// <returns>A durable task that completes when the external event is received.</returns>
        /// <exception cref="TimeoutException">
        /// The external event was not received before the timeout expired.
        /// </exception>
        public static Task WaitForExternalEvent(this IDurableOrchestrationContext context, string name, TimeSpan timeout)
        {
            return context.WaitForExternalEvent<object>(name, timeout);
        }

        /// <summary>
        /// Waits asynchronously for an event to be raised with name <paramref name="name"/>.
        /// </summary>
        /// <remarks>
        /// External clients can raise events to a waiting orchestration instance using
        /// <see cref="IDurableOrchestrationClient.RaiseEventAsync(string, string, object)"/> with the object parameter set to <c>null</c>.
        /// </remarks>
        /// <param name="context">The context object.</param>
        /// <param name="name">The name of the event to wait for.</param>
        /// <param name="timeout">The duration after which to throw a TimeoutException.</param>
        /// <param name="cancelToken">The <c>CancellationToken</c> to use for cancelling <paramref name="timeout"/>'s internal timer.</param>
        /// <returns>A durable task that completes when the external event is received.</returns>
        /// <exception cref="TimeoutException">
        /// The external event was not received before the timeout expired.
        /// </exception>
        public static Task WaitForExternalEvent(this IDurableOrchestrationContext context, string name, TimeSpan timeout, CancellationToken cancelToken)
        {
            return context.WaitForExternalEvent<object>(name, timeout, cancelToken);
        }

        /// <summary>
        /// Waits asynchronously for an event to be raised with name <paramref name="name"/> and returns the event data.
        /// </summary>
        /// <remarks>
        /// External clients can raise events to a waiting orchestration instance using
        /// <see cref="IDurableOrchestrationClient.RaiseEventAsync(string, string, object)"/>.
        /// </remarks>
        /// <param name="context">The context object.</param>
        /// <param name="name">The name of the event to wait for.</param>
        /// <param name="timeout">The duration after which to throw a TimeoutException.</param>
        /// <typeparam name="T">Any serializeable type that represents the JSON event payload.</typeparam>
        /// <returns>A durable task that completes when the external event is received.</returns>
        /// <exception cref="TimeoutException">
        /// The external event was not received before the timeout expired.
        /// </exception>
        public static Task<T> WaitForExternalEvent<T>(this IDurableOrchestrationContext context, string name, TimeSpan timeout)
        {
            return context.WaitForExternalEvent<T>(name, timeout, CancellationToken.None);
        }

        /// <summary>
        /// Waits asynchronously for an event to be raised with name <paramref name="name"/> and returns the event data.
        /// </summary>
        /// <remarks>
        /// External clients can raise events to a waiting orchestration instance using
        /// <see cref="IDurableOrchestrationClient.RaiseEventAsync(string, string, object)"/>.
        /// </remarks>
        /// <param name="context">The context object.</param>
        /// <param name="name">The name of the event to wait for.</param>
        /// <param name="timeout">The duration after which to return the value in the <paramref name="defaultValue"/> parameter.</param>
        /// <param name="defaultValue">The default value to return if the timeout expires before the external event is received.</param>
        /// <typeparam name="T">Any serializeable type that represents the JSON event payload.</typeparam>
        /// <returns>A durable task that completes when the external event is received, or returns the value of <paramref name="defaultValue"/>
        /// if the timeout expires.</returns>
        public static Task<T> WaitForExternalEvent<T>(this IDurableOrchestrationContext context, string name, TimeSpan timeout, T defaultValue)
        {
            return context.WaitForExternalEvent<T>(name, timeout, defaultValue, CancellationToken.None);
        }

        /// <summary>
        /// Calls an operation on an entity and returns the result asynchronously.
        /// </summary>
        /// <typeparam name="TResult">The JSON-serializable result type of the operation.</typeparam>
        /// <param name="context">The context object.</param>
        /// <param name="entityId">The target entity.</param>
        /// <param name="operationName">The name of the operation.</param>
        /// <returns>A task representing the result of the operation.</returns>
        public static Task<TResult> CallEntityAsync<TResult>(this IDurableOrchestrationContext context, EntityId entityId, string operationName)
        {
            return context.CallEntityAsync<TResult>(entityId, operationName, null);
        }

        /// <summary>
        /// Calls an operation on an entity and waits for it to complete.
        /// </summary>
        /// <param name="context">The context object.</param>
        /// <param name="entityId">The target entity.</param>
        /// <param name="operationName">The name of the operation.</param>
        /// <returns>A task representing the completion of the operation on the entity.</returns>
        public static Task CallEntityAsync(this IDurableOrchestrationContext context, EntityId entityId, string operationName)
        {
            return context.CallEntityAsync<object>(entityId, operationName, null);
        }

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
        /// <param name="client">The client object.</param>
        /// <param name="request">The HTTP request that triggered the current function.</param>
        /// <param name="instanceId">The unique ID of the instance to check.</param>
        /// <returns>An HTTP response which may include a 202 and location header or a 200 with the durable function output in the response body.</returns>
        public static Task<HttpResponseMessage> WaitForCompletionOrCreateCheckStatusResponseAsync(
            this IDurableOrchestrationClient client,
            HttpRequestMessage request,
            string instanceId)
        {
            return client.WaitForCompletionOrCreateCheckStatusResponseAsync(
                request,
                instanceId,
                timeout: TimeSpan.FromSeconds(10));
        }

#if !FUNCTIONS_V1
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
        /// <param name="client">The client object.</param>
        /// <param name="request">The HTTP request that triggered the current function.</param>
        /// <param name="instanceId">The unique ID of the instance to check.</param>
        /// <returns>An HTTP response which may include a 202 and location header or a 200 with the durable function output in the response body.</returns>
        public static Task<IActionResult> WaitForCompletionOrCreateCheckStatusResponseAsync(
            this IDurableOrchestrationClient client,
            HttpRequest request,
            string instanceId)
        {
            return client.WaitForCompletionOrCreateCheckStatusResponseAsync(
                request,
                instanceId,
                timeout: TimeSpan.FromSeconds(10));
        }
#endif

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
        /// <param name="client">The client object.</param>
        /// <param name="request">The HTTP request that triggered the current function.</param>
        /// <param name="instanceId">The unique ID of the instance to check.</param>
        /// <param name="timeout">Total allowed timeout for output from the durable function. The default value is 10 seconds.</param>
        /// <returns>An HTTP response which may include a 202 and location header or a 200 with the durable function output in the response body.</returns>
        public static Task<HttpResponseMessage> WaitForCompletionOrCreateCheckStatusResponseAsync(
            this IDurableOrchestrationClient client,
            HttpRequestMessage request,
            string instanceId,
            TimeSpan timeout)
        {
            return client.WaitForCompletionOrCreateCheckStatusResponseAsync(
                request,
                instanceId,
                timeout,
                retryInterval: TimeSpan.FromSeconds(1));
        }

#if !FUNCTIONS_V1
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
        /// <param name="client">The client object.</param>
        /// <param name="request">The HTTP request that triggered the current function.</param>
        /// <param name="instanceId">The unique ID of the instance to check.</param>
        /// <param name="timeout">Total allowed timeout for output from the durable function. The default value is 10 seconds.</param>
        /// <returns>An HTTP response which may include a 202 and location header or a 200 with the durable function output in the response body.</returns>
        public static Task<IActionResult> WaitForCompletionOrCreateCheckStatusResponseAsync(
            this IDurableOrchestrationClient client,
            HttpRequest request,
            string instanceId,
            TimeSpan timeout)
        {
            return client.WaitForCompletionOrCreateCheckStatusResponseAsync(
                request,
                instanceId,
                timeout,
                retryInterval: TimeSpan.FromSeconds(1));
        }
#endif

        /// <summary>
        /// Starts a new execution of the specified orchestrator function.
        /// </summary>
        /// <param name="client">The client object.</param>
        /// <param name="orchestratorFunctionName">The name of the orchestrator function to start.</param>
        /// <param name="instanceId">The ID to use for the new orchestration instance.</param>
        /// <returns>A task that completes when the orchestration is started. The task contains the instance id of the started
        /// orchestratation instance.</returns>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        public static Task<string> StartNewAsync(
            this IDurableOrchestrationClient client,
            string orchestratorFunctionName,
            string instanceId)
        {
            return client.StartNewAsync<object>(orchestratorFunctionName, instanceId, null);
        }

        /// <summary>
        /// Starts a new execution of the specified orchestrator function.
        /// </summary>
        /// <param name="client">The client object.</param>
        /// <param name="orchestratorFunctionName">The name of the orchestrator function to start.</param>
        /// <param name="input">JSON-serializeable input value for the orchestrator function.</param>
        /// <typeparam name="T">The type of the input value for the orchestrator function.</typeparam>
        /// <returns>A task that completes when the orchestration is started. The task contains the instance id of the started
        /// orchestratation instance.</returns>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        public static Task<string> StartNewAsync<T>(
            this IDurableOrchestrationClient client,
            string orchestratorFunctionName,
            T input)
            where T : class
        {
            return client.StartNewAsync(orchestratorFunctionName, string.Empty, input);
        }

        /// <summary>
        /// Starts a new execution of the specified orchestrator function.
        /// </summary>
        /// <param name="client">The client object.</param>
        /// <param name="orchestratorFunctionName">The name of the orchestrator function to start.</param>
        /// <returns>A task that completes when the orchestration is started. The task contains the instance id of the started
        /// orchestratation instance.</returns>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        public static Task<string> StartNewAsync(
            this IDurableOrchestrationClient client,
            string orchestratorFunctionName)
        {
            return client.StartNewAsync<object>(orchestratorFunctionName, null);
        }

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
        /// <param name="client">The client object.</param>
        /// <param name="instanceId">The ID of the orchestration instance that will handle the event.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <returns>A task that completes when the event notification message has been enqueued.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate", Justification = "This method does not work with the .NET Framework event model.")]
        public static Task RaiseEventAsync(
            this IDurableOrchestrationClient client,
            string instanceId,
            string eventName)
        {
            return client.RaiseEventAsync(instanceId, eventName, null);
        }

        /// <summary>
        /// Gets the status of the specified orchestration instance.
        /// </summary>
        /// <param name="client">The client object.</param>
        /// <param name="instanceId">The ID of the orchestration instance to query.</param>
        /// <returns>Returns a task which completes when the status has been fetched.</returns>
        public static Task<DurableOrchestrationStatus> GetStatusAsync(this IDurableOrchestrationClient client, string instanceId)
        {
            return client.GetStatusAsync(instanceId, showHistory: false);
        }

        /// <summary>
        /// Gets the status of the specified orchestration instance.
        /// </summary>
        /// <param name="client">The client object.</param>
        /// <param name="instanceId">The ID of the orchestration instance to query.</param>
        /// <param name="showHistory">Boolean marker for including execution history in the response.</param>
        /// <returns>Returns a task which completes when the status has been fetched.</returns>
        public static Task<DurableOrchestrationStatus> GetStatusAsync(this IDurableOrchestrationClient client, string instanceId, bool showHistory)
        {
            return client.GetStatusAsync(instanceId, showHistory, showHistoryOutput: false, showInput: true);
        }

        /// <summary>
        /// Returns an instance of ILogger that is replay safe, ensuring the logger logs only when the orchestrator
        /// is not replaying that line of code.
        /// </summary>
        /// <param name="context">The context object.</param>
        /// <param name="logger">An instance of ILogger.</param>
        /// <returns>An instance of a replay safe ILogger.</returns>
        public static ILogger CreateReplaySafeLogger(this IDurableOrchestrationContext context, ILogger logger)
        {
            return new ReplaySafeLogger(context, logger);
        }
    }
}
