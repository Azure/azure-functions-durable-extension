// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Functionality available in all deterministic execution contexts, such as orchestrations or entity operations.
    /// </summary>
    public interface IDeterministicExecutionContext
    {
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
        /// Signals an entity to perform an operation, without waiting for a response. Any result or exception is ignored (fire and forget).
        /// </summary>
        /// <param name="entity">The target entity.</param>
        /// <param name="operationName">The name of the operation.</param>
        /// <param name="operationContent">The content for the operation.</param>
        void SignalEntity(EntityId entity, string operationName, object operationContent = null);

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
    }
}
