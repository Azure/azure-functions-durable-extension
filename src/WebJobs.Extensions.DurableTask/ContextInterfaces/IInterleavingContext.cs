// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Provides functionality available to orchestration code and reentrant actor code.
    /// </summary>
    public interface IInterleavingContext : IDeterministicExecutionContext
    {
        /// <summary>
        /// Calls an operation on an actor, passing an argument, and returns the result asynchronously.
        /// </summary>
        /// <typeparam name="TResult">The JSON-serializable result type of the operation.</typeparam>
        /// <param name="actorId">The target actor.</param>
        /// <param name="operationName">The name of the operation.</param>
        /// <param name="operationContent">The content (input argument) of the operation.</param>
        /// <returns>A task representing the result of the operation.</returns>
        /// <exception cref="LockingRulesViolationException">if the context already holds some locks, but not the one for <paramref name="actorId"/>.</exception>
        Task<TResult> CallActorAsync<TResult>(ActorId actorId, string operationName, object operationContent);

        /// <summary>
        /// Calls an operation on an actor, passing an argument, and waits for it to complete.
        /// </summary>
        /// <param name="actorId">The target actor.</param>
        /// <param name="operationName">The name of the operation.</param>
        /// <param name="operationContent">The content for the operation.</param>
        /// <returns>A task representing the completion of the operation on the actor.</returns>
        /// <exception cref="LockingRulesViolationException">if the context already holds some locks, but not the one for <paramref name="actorId"/>.</exception>
        Task CallActorAsync(ActorId actorId, string operationName, object operationContent);

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
        Task<T> CreateTimer<T>(DateTime fireAt, T state, CancellationToken cancelToken);

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
        /// Waits asynchronously for an event to be raised with name <paramref name="name"/> and returns the event data.
        /// </summary>
        /// <remarks>
        /// External clients can raise events to a waiting orchestration instance using
        /// <see cref="IDurableOrchestrationClient.RaiseEventAsync(string, string, object)"/>.
        /// </remarks>
        /// <param name="name">The name of the event to wait for.</param>
        /// <param name="timeout">The duration after which to throw a TimeoutException.</param>
        /// <typeparam name="T">Any serializeable type that represents the JSON event payload.</typeparam>
        /// <returns>A durable task that completes when the external event is received.</returns>
        /// <exception cref="TimeoutException">
        /// The external event was not received before the timeout expired.
        /// </exception>
        Task<T> WaitForExternalEvent<T>(string name, TimeSpan timeout);

        /// <summary>
        /// Waits asynchronously for an event to be raised with name <paramref name="name"/> and returns the event data.
        /// </summary>
        /// <remarks>
        /// External clients can raise events to a waiting orchestration instance using
        /// <see cref="IDurableOrchestrationClient.RaiseEventAsync(string, string, object)"/>.
        /// </remarks>
        /// <param name="name">The name of the event to wait for.</param>
        /// <param name="timeout">The duration after which to return the value in the <paramref name="defaultValue"/> parameter.</param>
        /// <param name="defaultValue">The default value to return if the timeout expires before the external event is received.</param>
        /// <typeparam name="T">Any serializeable type that represents the JSON event payload.</typeparam>
        /// <returns>A durable task that completes when the external event is received, or returns the value of <paramref name="defaultValue"/>
        /// if the timeout expires.</returns>
        Task<T> WaitForExternalEvent<T>(string name, TimeSpan timeout, T defaultValue);

        /// <summary>
        /// Acquires one or more locks, for the specified actors.
        /// </summary>
        /// <remarks>
        /// Locks can only be acquired if the current context does not hold any locks already.
        /// </remarks>
        /// <param name="actors">The actors whose locks should be acquired.</param>
        /// <returns>An IDisposable that releases the lock when disposed.</returns>
        /// <exception cref="LockingRulesViolationException">if the context already holds some locks.</exception>
        Task<IDisposable> LockAsync(params ActorId[] actors);
    }
}
