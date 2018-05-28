// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Abstract base class for <see cref="DurableOrchestrationContext"/>.
    /// </summary>
    public abstract class DurableOrchestrationContextBase
    {
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
        public abstract string InstanceId { get; }

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
        public virtual string ParentInstanceId { get; internal set; }

        /// <summary>
        /// Gets the current date/time in a way that is safe for use by orchestrator functions.
        /// </summary>
        /// <remarks>
        /// This date/time value is derived from the orchestration history. It always returns the same value
        /// at specific points in the orchestrator function code, making it deterministic and safe for replay.
        /// </remarks>
        /// <value>The orchestration's current date/time in UTC.</value>
        public abstract DateTime CurrentUtcDateTime { get; }

        /// <summary>
        /// Gets a value indicating whether the orchestrator function is currently replaying itself.
        /// </summary>
        /// <remarks>
        /// This property is useful when there is logic that needs to run only when the orchestrator function
        /// is *not* replaying. For example, certain types of application logging may become too noisy when duplicated
        /// as part of orchestrator function replay. The orchestrator code could check to see whether the function is
        /// being replayed and then issue the log statements when this value is <c>false</c>.
        /// </remarks>
        /// <value>
        /// <c>true</c> if the orchestrator function is currently being replayed; otherwise <c>false</c>.
        /// </value>
        public virtual bool IsReplaying => false;

        /// <summary>
        /// Gets the input of the current orchestrator function as a deserialized value.
        /// </summary>
        /// <typeparam name="T">Any data contract type that matches the JSON input.</typeparam>
        /// <returns>The deserialized input value.</returns>
        public abstract T GetInput<T>();

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
        public virtual Task CallActivityAsync(string functionName, object input)
        {
            return this.CallActivityAsync<object>(functionName, input);
        }

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
        public virtual Task CallActivityWithRetryAsync(string functionName, RetryOptions retryOptions, object input)
        {
            return this.CallActivityWithRetryAsync<object>(functionName, retryOptions, input);
        }

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
        public abstract Task<TResult> CallActivityAsync<TResult>(string functionName, object input);

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
        public abstract Task<TResult> CallActivityWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, object input);

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
        public virtual Task CallSubOrchestratorAsync(string functionName, object input)
        {
            return this.CallSubOrchestratorAsync<object>(functionName, input);
        }

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
        public virtual Task CallSubOrchestratorAsync(string functionName, string instanceId, object input)
        {
            return this.CallSubOrchestratorAsync<object>(functionName, instanceId, input);
        }

        /// <summary>
        /// Schedules an orchestration function named <paramref name="functionName"/> for execution.
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
        /// The activity function failed with an unhandled exception.
        /// </exception>
        public virtual Task<TResult> CallSubOrchestratorAsync<TResult>(string functionName, object input)
        {
            return this.CallSubOrchestratorAsync<TResult>(functionName, null, input);
        }

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
        public abstract Task<TResult> CallSubOrchestratorAsync<TResult>(string functionName, string instanceId, object input);

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
        public virtual Task CallSubOrchestratorWithRetryAsync(string functionName, RetryOptions retryOptions, object input)
        {
            return this.CallSubOrchestratorWithRetryAsync<object>(functionName, retryOptions, null, input);
        }

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
        public virtual Task CallSubOrchestratorWithRetryAsync(string functionName, RetryOptions retryOptions, string instanceId, object input)
        {
            return this.CallSubOrchestratorWithRetryAsync<object>(functionName, retryOptions, instanceId, input);
        }

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
        public virtual Task<TResult> CallSubOrchestratorWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, object input)
        {
            return this.CallSubOrchestratorWithRetryAsync<TResult>(functionName, retryOptions, null, input);
        }

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
        public abstract Task<TResult> CallSubOrchestratorWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, string instanceId, object input);

        /// <summary>
        /// Creates a durable timer that expires at a specified time.
        /// </summary>
        /// <remarks>
        /// All durable timers created using this method must either expire or be cancelled
        /// using the <paramref name="cancelToken"/> before the orchestrator function completes.
        /// Otherwise the underlying framework will keep the instance alive until the timer expires.
        /// </remarks>
        /// <param name="fireAt">The time at which the timer should expire.</param>
        /// <param name="cancelToken">The <c>CancellationToken</c> to use for cancelling the timer.</param>
        /// <returns>A durable task that completes when the durable timer expires.</returns>
        public virtual Task CreateTimer(DateTime fireAt, CancellationToken cancelToken)
        {
            return this.CreateTimer<object>(fireAt, null, cancelToken);
        }

        /// <summary>
        /// Creates a durable timer that expires at a specified time.
        /// </summary>
        /// <remarks>
        /// All durable timers created using this method must either expire or be cancelled
        /// using the <paramref name="cancelToken"/> before the orchestrator function completes.
        /// Otherwise the underlying framework will keep the instance alive until the timer expires.
        /// </remarks>
        /// <typeparam name="T">The type of <paramref name="state"/>.</typeparam>
        /// <param name="fireAt">The time at which the timer should expire.</param>
        /// <param name="state">Any state to be preserved by the timer.</param>
        /// <param name="cancelToken">The <c>CancellationToken</c> to use for cancelling the timer.</param>
        /// <returns>A durable task that completes when the durable timer expires.</returns>
        public abstract Task<T> CreateTimer<T>(DateTime fireAt, T state, CancellationToken cancelToken);

        /// <summary>
        /// Waits asynchronously for an event to be raised with name <paramref name="name"/> and returns the event data.
        /// </summary>
        /// <remarks>
        /// External clients can raise events to a waiting orchestration instance using
        /// <see cref="DurableOrchestrationClient.RaiseEventAsync"/>.
        /// </remarks>
        /// <param name="name">The name of the event to wait for.</param>
        /// <typeparam name="T">Any serializeable type that represents the JSON event payload.</typeparam>
        /// <returns>A durable task that completes when the external event is received.</returns>
        public abstract Task<T> WaitForExternalEvent<T>(string name);

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
        public abstract void ContinueAsNew(object input);

        /// <summary>
        /// Sets the JSON-serializeable status of the current orchestrator function.
        /// </summary>
        /// <remarks>
        /// The <paramref name="customStatusObject"/> value is serialized to JSON and will
        /// be made available to the orchestration status query APIs. The serialized JSON
        /// value must not exceed 16 KB of UTF-16 encoded text.
        /// </remarks>
        /// <param name="customStatusObject">The JSON-serializeable value to use as the orchestrator function's custom status.</param>
        public abstract void SetCustomStatus(object customStatusObject);
    }
}
