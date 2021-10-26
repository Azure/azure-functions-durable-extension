// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Provides functionality available to orchestration code.
    /// </summary>
    public interface IDurableOrchestrationContext
    {
        /// <summary>
        /// Gets the name of the current orchestration function.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the instance ID of the currently executing orchestration.
        /// </summary>
        /// <remarks>
        /// The instance ID is generated and fixed when the orchestrator function is scheduled. It can be either
        /// auto-generated, in which case it is formatted as a GUID, or it can be user-specified with any format.
        /// </remarks>
        /// <value>
        /// The ID of the current orchestration instance.
        /// </value>
        string InstanceId { get; }

        /// <summary>
        /// Gets the parent instance ID of the currently executing sub-orchestration.
        /// </summary>
        /// <remarks>
        /// The parent instance ID is generated and fixed when the parent orchestrator function is scheduled. It can be either
        /// auto-generated, in which case it is formatted as a GUID, or it can be user-specified with any format.
        /// </remarks>
        /// <value>
        /// The ID of the parent orchestration of the current sub-orchestration instance. The value will be available only in sub-orchestrations.
        /// </value>
        string ParentInstanceId { get; }

        /// <summary>
        /// Gets the current date/time in a way that is safe for use in orchestrations and entity operations.
        /// </summary>
        /// <remarks>
        /// This date/time value is derived from the orchestration or entity history. It always returns the same value
        /// at specific points in the orchestrator function code, making it deterministic and safe for replay.
        /// </remarks>
        /// <value>The orchestration or entity's current date/time in UTC.</value>
        DateTime CurrentUtcDateTime { get; }

        /// <summary>
        /// Gets a value indicating whether the orchestration or operation is currently replaying itself.
        /// </summary>
        /// <remarks>
        /// This property is useful when there is logic that needs to run only when *not* replaying. For example, certain types of application logging may become too noisy when duplicated
        /// as part of replay. The application code could check to see whether the function is
        /// being replayed and then issue the log statements when this value is <c>false</c>.
        /// </remarks>
        /// <value>
        /// <c>true</c> if the orchestration or operation is currently being replayed; otherwise <c>false</c>.
        /// </value>
        bool IsReplaying { get; }

        /// <summary>
        /// Gets the input of the current orchestrator function as a deserialized value.
        /// </summary>
        /// <typeparam name="TInput">Any data contract type that matches the JSON input.</typeparam>
        /// <returns>The deserialized input value.</returns>
        TInput GetInput<TInput>();

        /// <summary>
        /// Sets the output for the current orchestration.
        /// </summary>
        /// <param name="output">The JSON-serializeable output of the orchestration.</param>
        void SetOutput(object output);

        /// <summary>
        /// Restarts the orchestration by clearing its history.
        /// </summary>
        /// <remarks>
        /// <para>Large orchestration histories can consume a lot of memory and cause delays in
        /// instance load times. This method can be used to periodically truncate the stored
        /// history of an orchestration instance.</para>
        /// <para>Note that any unprocessed external events will be discarded when an orchestration
        /// instance restarts itself using this method.</para>
        /// </remarks>
        /// <param name="input">The JSON-serializeable data to re-initialize the instance with.</param>
        /// <param name="preserveUnprocessedEvents">
        /// If set to <c>true</c>, re-adds any unprocessed external events into the new execution
        /// history when the orchestration instance restarts. If <c>false</c>, any unprocessed
        /// external events will be discarded when the orchestration instance restarts.
        /// </param>
        void ContinueAsNew(object input, bool preserveUnprocessedEvents = false);

        /// <summary>
        /// Sets the JSON-serializeable status of the current orchestrator function.
        /// </summary>
        /// <remarks>
        /// The <paramref name="customStatusObject"/> value is serialized to JSON and will
        /// be made available to the orchestration status query APIs. The serialized JSON
        /// value must not exceed 16 KB of UTF-16 encoded text.
        /// </remarks>
        /// <param name="customStatusObject">The JSON-serializeable value to use as the orchestrator function's custom status.</param>
        void SetCustomStatus(object customStatusObject);

        /// <summary>
        /// Makes an HTTP call to the specified uri.
        /// </summary>
        /// <param name="method">HttpMethod used for api call.</param>
        /// <param name="uri">uri used to make the HTTP call.</param>
        /// <param name="content">Content passed in the HTTP request.</param>
        /// <param name="retryOptions">The retry option for the HTTP task.</param>
        /// <returns>A <see cref="Task{DurableHttpResponse}"/>Result of the HTTP call.</returns>
        Task<DurableHttpResponse> CallHttpAsync(HttpMethod method, Uri uri, string content = null, HttpRetryOptions retryOptions = null);

        /// <summary>
        /// Makes an HTTP call using the information in the DurableHttpRequest.
        /// </summary>
        /// <param name="req">The DurableHttpRequest used to make the HTTP call.</param>
        /// <returns>A <see cref="Task{DurableHttpResponse}"/>Result of the HTTP call.</returns>
        Task<DurableHttpResponse> CallHttpAsync(DurableHttpRequest req);

        /// <summary>
        /// Calls an operation on an entity and returns the result asynchronously.
        /// </summary>
        /// <typeparam name="TResult">The JSON-serializable result type of the operation.</typeparam>
        /// <param name="entityId">The target entity.</param>
        /// <param name="operationName">The name of the operation.</param>
        /// <returns>A task representing the result of the operation.</returns>
        Task<TResult> CallEntityAsync<TResult>(EntityId entityId, string operationName);

        /// <summary>
        /// Calls an operation on an entity and waits for it to complete.
        /// </summary>
        /// <param name="entityId">The target entity.</param>
        /// <param name="operationName">The name of the operation.</param>
        /// <returns>A task representing the completion of the operation on the entity.</returns>
        Task CallEntityAsync(EntityId entityId, string operationName);

        /// <summary>
        /// Calls an operation on an entity, passing an argument, and returns the result asynchronously.
        /// </summary>
        /// <typeparam name="TResult">The JSON-serializable result type of the operation.</typeparam>
        /// <param name="entityId">The target entity.</param>
        /// <param name="operationName">The name of the operation.</param>
        /// <param name="operationInput">The input for the operation.</param>
        /// <returns>A task representing the result of the operation.</returns>
        /// <exception cref="LockingRulesViolationException">if the context already holds some locks, but not the one for <paramref name="entityId"/>.</exception>
        Task<TResult> CallEntityAsync<TResult>(EntityId entityId, string operationName, object operationInput);

        /// <summary>
        /// Calls an operation on an entity, passing an argument, and waits for it to complete.
        /// </summary>
        /// <param name="entityId">The target entity.</param>
        /// <param name="operationName">The name of the operation.</param>
        /// <param name="operationInput">The input for the operation.</param>
        /// <returns>A task representing the completion of the operation on the entity.</returns>
        /// <exception cref="LockingRulesViolationException">if the context already holds some locks, but not the one for <paramref name="entityId"/>.</exception>
        Task CallEntityAsync(EntityId entityId, string operationName, object operationInput);

        /// <summary>
        /// Schedules an orchestrator function named <paramref name="functionName"/> for execution.
        /// </summary>
        /// <typeparam name="TResult">The return type of the scheduled orchestrator function.</typeparam>
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
        Task<TResult> CallSubOrchestratorAsync<TResult>(string functionName, object input);

        /// <summary>
        /// Schedules an orchestration function named <paramref name="functionName"/> for execution.
        /// </summary>
        /// <typeparam name="TResult">The return type of the scheduled orchestrator function.</typeparam>
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
        Task<TResult> CallSubOrchestratorAsync<TResult>(string functionName, string instanceId, object input);

        /// <summary>
        /// Schedules an orchestrator function named <paramref name="functionName"/> for execution.
        /// </summary>
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
        Task CallSubOrchestratorAsync(string functionName, object input);

        /// <summary>
        /// Schedules an orchestrator function named <paramref name="functionName"/> for execution.
        /// </summary>
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
        Task CallSubOrchestratorAsync(string functionName, string instanceId, object input);

        /// <summary>
        /// Schedules an orchestrator function named <paramref name="functionName"/> for execution with retry options.
        /// </summary>
        /// <typeparam name="TResult">The return type of the scheduled orchestrator function.</typeparam>
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
        Task<TResult> CallSubOrchestratorWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, string instanceId, object input);

        /// <summary>
        /// Schedules an orchestrator function named <paramref name="functionName"/> for execution with retry options.
        /// </summary>
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
        Task CallSubOrchestratorWithRetryAsync(string functionName, RetryOptions retryOptions, object input);

        /// <summary>
        /// Schedules an orchestrator function named <paramref name="functionName"/> for execution with retry options.
        /// </summary>
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
        Task CallSubOrchestratorWithRetryAsync(string functionName, RetryOptions retryOptions, string instanceId, object input);

        /// <summary>
        /// Schedules an orchestrator function named <paramref name="functionName"/> for execution with retry options.
        /// </summary>
        /// <typeparam name="TResult">The return type of the scheduled orchestrator function.</typeparam>
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
        Task<TResult> CallSubOrchestratorWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, object input);

        /// <summary>
        /// Creates a durable timer that expires at a specified time.
        /// </summary>
        /// <remarks>
        /// All durable timers created using this method must either expire or be cancelled
        /// using the <paramref name="cancelToken"/> before the orchestrator function completes.
        ///  Otherwise the underlying framework will keep the instance in the "Running" state
        ///  even after the orchestrator function has completed.
        /// </remarks>
        /// <typeparam name="T">The type of <paramref name="state"/>.</typeparam>
        /// <param name="fireAt">The time at which the timer should expire.</param>
        /// <param name="state">Any state to be preserved by the timer.</param>
        /// <param name="cancelToken">The <c>CancellationToken</c> to use for cancelling the timer.</param>
        /// <returns>A durable task that completes when the durable timer expires.</returns>
        Task<T> CreateTimer<T>(DateTime fireAt, T state, CancellationToken cancelToken);

        /// <summary>
        /// Creates a durable timer that expires at a specified time.
        /// </summary>
        /// <remarks>
        /// All durable timers created using this method must either expire or be cancelled
        /// using the <paramref name="cancelToken"/> before the orchestrator function completes.
        ///  Otherwise the underlying framework will keep the instance in the "Running" state
        ///  even after the orchestrator function has completed.
        /// </remarks>
        /// <param name="fireAt">The time at which the timer should expire.</param>
        /// <param name="cancelToken">The <c>CancellationToken</c> to use for cancelling the timer.</param>
        /// <returns>A durable task that completes when the durable timer expires.</returns>
        Task CreateTimer(DateTime fireAt, CancellationToken cancelToken);

        /// <summary>
        /// Waits asynchronously for an event to be raised with name <paramref name="name"/> and returns the event data.
        /// </summary>
        /// <remarks>
        /// External clients can raise events to a waiting orchestration instance using
        /// <see cref="IDurableOrchestrationClient.RaiseEventAsync(string, string, object)"/>.
        /// </remarks>
        /// <param name="name">The name of the event to wait for.</param>
        /// <typeparam name="T">Any serializeable type that represents the JSON event payload.</typeparam>
        /// <returns>A durable task that completes when the external event is received.</returns>
        Task<T> WaitForExternalEvent<T>(string name);

        /// <summary>
        /// Waits asynchronously for an event to be raised with name <paramref name="name"/>.
        /// </summary>
        /// <remarks>
        /// External clients can raise events to a waiting orchestration instance using
        /// <see cref="IDurableOrchestrationClient.RaiseEventAsync(string, string, object)"/> with the object parameter set to <c>null</c>.
        /// </remarks>
        /// <param name="name">The name of the event to wait for.</param>
        /// <returns>A durable task that completes when the external event is received.</returns>
        Task WaitForExternalEvent(string name);

        /// <summary>
        /// Waits asynchronously for an event to be raised with name <paramref name="name"/>.
        /// </summary>
        /// <remarks>
        /// External clients can raise events to a waiting orchestration instance using
        /// <see cref="IDurableOrchestrationClient.RaiseEventAsync(string, string, object)"/> with the object parameter set to <c>null</c>.
        /// </remarks>
        /// <param name="name">The name of the event to wait for.</param>
        /// <param name="timeout">The duration after which to throw a TimeoutException.</param>
        /// <param name="cancelToken">The <c>CancellationToken</c> to use for cancelling <paramref name="timeout"/>'s internal timer.</param>
        /// <returns>A durable task that completes when the external event is received.</returns>
        /// <exception cref="TimeoutException">
        /// The external event was not received before the timeout expired.
        /// </exception>
        Task WaitForExternalEvent(string name, TimeSpan timeout, CancellationToken cancelToken = default(CancellationToken));

        /// <summary>
        /// Waits asynchronously for an event to be raised with name <paramref name="name"/> and returns the event data.
        /// </summary>
        /// <remarks>
        /// External clients can raise events to a waiting orchestration instance using
        /// <see cref="IDurableOrchestrationClient.RaiseEventAsync(string, string, object)"/>.
        /// </remarks>
        /// <param name="name">The name of the event to wait for.</param>
        /// <param name="timeout">The duration of time to wait for the event.</param>
        /// <param name="cancelToken">The <c>CancellationToken</c> to use for cancelling <paramref name="timeout"/>'s internal timer.</param>
        /// <typeparam name="T">Any serializeable type that represents the JSON event payload.</typeparam>
        /// <returns>A durable task that completes when the external event is received, or throws a timeout exception"/>
        /// if the timeout expires.</returns>
        Task<T> WaitForExternalEvent<T>(string name, TimeSpan timeout, CancellationToken cancelToken = default(CancellationToken));

        /// <summary>
        /// Waits asynchronously for an event to be raised with name <paramref name="name"/> and returns the event data.
        /// </summary>
        /// <remarks>
        /// External clients can raise events to a waiting orchestration instance using
        /// <see cref="IDurableOrchestrationClient.RaiseEventAsync(string, string, object)"/>.
        /// </remarks>
        /// <param name="name">The name of the event to wait for.</param>
        /// <param name="timeout">The duration of time to wait for the event.</param>
        /// <param name="defaultValue">If specified, the default value to return if the timeout expires before the external event is received.
        /// Otherwise, a timeout exception will be thrown instead.</param>
        /// <param name="cancelToken">The <c>CancellationToken</c> to use for cancelling <paramref name="timeout"/>'s internal timer.</param>
        /// <typeparam name="T">Any serializeable type that represents the JSON event payload.</typeparam>
        /// <returns>A durable task that completes when the external event is received, or returns the value of <paramref name="defaultValue"/>
        /// if the timeout expires.</returns>
        Task<T> WaitForExternalEvent<T>(string name, TimeSpan timeout, T defaultValue, CancellationToken cancelToken = default(CancellationToken));

        /// <summary>
        /// Acquires one or more locks, for the specified entities.
        /// </summary>
        /// <remarks>
        /// Locks can only be acquired if the current context does not hold any locks already.
        /// </remarks>
        /// <param name="entities">The entities whose locks should be acquired.</param>
        /// <returns>An IDisposable that releases the lock when disposed.</returns>
        /// <exception cref="LockingRulesViolationException">if the context already holds some locks.</exception>
        Task<IDisposable> LockAsync(params EntityId[] entities);

        /// <summary>
        /// Determines whether the current context is locked, and if so, what locks are currently owned.
        /// </summary>
        /// <param name="ownedLocks">The collection of owned locks.</param>
        /// <remarks>
        /// Note that the collection of owned locks can be empty even if the context is locked. This happens
        /// if an orchestration calls a suborchestration without lending any locks.
        /// </remarks>
        /// <returns><c>true</c> if the context already holds some locks.</returns>
        bool IsLocked(out IReadOnlyList<EntityId> ownedLocks);

        /// <summary>
        /// Creates a new GUID that is safe for replay within an orchestration or operation.
        /// </summary>
        /// <remarks>
        /// The default implementation of this method creates a name-based UUID using the algorithm from
        /// RFC 4122 §4.3. The name input used to generate this value is a combination of the orchestration
        /// instance ID and an internally managed sequence number.
        /// </remarks>
        /// <returns>The new <see cref="Guid"/> value.</returns>
        Guid NewGuid();

        /// <summary>
        /// Schedules an activity function named <paramref name="functionName"/> for execution.
        /// </summary>
        /// <typeparam name="TResult">The return type of the scheduled activity function.</typeparam>
        /// <param name="functionName">The name of the activity function to call.</param>
        /// <param name="input">The JSON-serializeable input to pass to the activity function.</param>
        /// <returns>A durable task that completes when the called activity function completes or fails.</returns>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The current thread is different than the thread which started the orchestrator execution.
        /// </exception>
        /// <exception cref="FunctionFailedException">
        /// The activity function failed with an unhandled exception.
        /// </exception>
        Task<TResult> CallActivityAsync<TResult>(string functionName, object input);

        /// <summary>
        /// Schedules an activity function named <paramref name="functionName"/> for execution.
        /// </summary>
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
        Task CallActivityAsync(string functionName, object input);

        /// <summary>
        /// Schedules an activity function named <paramref name="functionName"/> for execution with retry options.
        /// </summary>
        /// <typeparam name="TResult">The return type of the scheduled activity function.</typeparam>
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
        Task<TResult> CallActivityWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, object input);

        /// <summary>
        /// Schedules an activity function named <paramref name="functionName"/> for execution with retry options.
        /// </summary>
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
        Task CallActivityWithRetryAsync(string functionName, RetryOptions retryOptions, object input);

        /// <summary>
        /// Signals an entity to perform an operation, without waiting for a response. Any result or exception is ignored (fire and forget).
        /// </summary>
        /// <param name="entity">The target entity.</param>
        /// <param name="operationName">The name of the operation.</param>
        /// <param name="operationInput">The input for the operation.</param>
        void SignalEntity(EntityId entity, string operationName, object operationInput = null);

        /// <summary>
        /// Signals an operation to be performed by an entity at a specified time. Any result or exception is ignored (fire and forget).
        /// </summary>
        /// <param name="entity">The target entity.</param>
        /// <param name="scheduledTimeUtc">The time at which to start the operation.</param>
        /// <param name="operationName">The name of the operation.</param>
        /// <param name="operationInput">The input for the operation.</param>
        void SignalEntity(EntityId entity, DateTime scheduledTimeUtc, string operationName, object operationInput = null);

        /// <summary>
        /// Schedules a orchestration function named <paramref name="functionName"/> for execution./>.
        /// Any result or exception is ignored (fire and forget).
        /// </summary>
        /// <param name="functionName">The name of the orchestrator function to call.</param>
        /// <param name="input">the input to pass to the orchestrator function.</param>
        /// <param name="instanceId">optionally, an instance id for the orchestration. By default, a random GUID is used.</param>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        /// <returns>The instance id of the new orchestration.</returns>
        string StartNewOrchestration(string functionName, object input, string instanceId = null);

        /// <summary>
        /// Create an entity proxy.
        /// </summary>
        /// <param name="entityKey">The target entity key.</param>
        /// <typeparam name="TEntityInterface">Entity interface.</typeparam>
        /// <returns>Entity proxy.</returns>
        TEntityInterface CreateEntityProxy<TEntityInterface>(string entityKey);

        /// <summary>
        /// Create an entity proxy.
        /// </summary>
        /// <param name="entityId">The target entity.</param>
        /// <typeparam name="TEntityInterface">Entity interface.</typeparam>
        /// <returns>Entity proxy.</returns>
        TEntityInterface CreateEntityProxy<TEntityInterface>(EntityId entityId);
    }
}
