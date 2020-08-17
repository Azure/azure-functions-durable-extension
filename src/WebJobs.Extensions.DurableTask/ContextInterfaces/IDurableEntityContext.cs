// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Provides functionality for application code implementing an entity operation.
    /// </summary>
    public interface IDurableEntityContext
    {
        /// <summary>
        /// Gets the name of the currently executing entity.
        /// </summary>
        string EntityName { get; }

        /// <summary>
        /// Gets the key of the currently executing entity.
        /// </summary>
        string EntityKey { get; }

        /// <summary>
        /// Gets the id of the currently executing entity.
        /// </summary>
        EntityId EntityId { get; }

        /// <summary>
        /// Gets the name of the operation that was called.
        /// </summary>
        /// <remarks>
        /// An operation invocation on an entity includes an operation name, which states what
        /// operation to perform, and optionally an operation input.
        /// </remarks>
        string OperationName { get; }

#if !FUNCTIONS_V1
        /// <summary>
        /// Contains function invocation context to assist with dependency injection at Entity construction time.
        /// </summary>
        FunctionBindingContext FunctionBindingContext { get; set; }
#endif

        /// <summary>
        /// Whether this entity has a state.
        /// </summary>
        bool HasState { get; }

        /// <summary>
        /// Gets the current state of this entity, for reading and/or updating.
        /// If this entity has no state yet, creates it.
        /// </summary>
        /// <typeparam name="TState">The JSON-serializable type of the entity state.</typeparam>
        /// <param name="initializer">Provides an initial value to use for the state, instead of default(<typeparamref name="TState"/>).</param>
        /// <returns>The current state of this entity.</returns>
        /// <exception cref="InvalidCastException">If the current state has an incompatible type.</exception>
        TState GetState<TState>(Func<TState> initializer = null);

        /// <summary>
        /// Sets the current state of this entity.
        /// </summary>
        /// <param name="state">The JSON-serializable state of the entity.</param>
        void SetState(object state);

        /// <summary>
        /// Deletes the state of this entity.
        /// </summary>
        void DeleteState();

        /// <summary>
        /// Gets the input for this operation, as a deserialized value.
        /// </summary>
        /// <typeparam name="TInput">The JSON-serializable type used for the operation input.</typeparam>
        /// <returns>The operation input, or default(<typeparamref name="TInput"/>) if none.</returns>
        /// <remarks>
        /// An operation invocation on an entity includes an operation name, which states what
        /// operation to perform, and optionally an operation input.
        /// </remarks>
        TInput GetInput<TInput>();

        /// <summary>
        /// Gets the input for this operation, as a deserialized value.
        /// </summary>
        /// <param name="inputType">The JSON-serializable type used for the operation input.</param>
        /// <returns>The operation input, or default(<paramref name="inputType"/>) if none.</returns>
        /// <remarks>
        /// An operation invocation on an entity includes an operation name, which states what
        /// operation to perform, and optionally an operation input.
        /// </remarks>
        object GetInput(Type inputType);

         /// <summary>
        /// Returns the given result to the caller of this operation.
        /// </summary>
        /// <param name="result">the result to return.</param>
        void Return(object result);

        /// <summary>
        /// Signals an entity to perform an operation, without waiting for a response. Any result or exception is ignored (fire and forget).
        /// </summary>
        /// <param name="entity">The target entity.</param>
        /// <param name="operationName">The name of the operation.</param>
        /// <param name="operationInput">The operation input.</param>
        void SignalEntity(EntityId entity, string operationName, object operationInput = null);

        /// <summary>
        /// Signals an entity to perform an operation, at a specified time. Any result or exception is ignored (fire and forget).
        /// </summary>
        /// <param name="entity">The target entity.</param>
        /// <param name="scheduledTimeUtc">The time at which to start the operation.</param>
        /// <param name="operationName">The name of the operation.</param>
        /// <param name="operationInput">The input for the operation.</param>
        void SignalEntity(EntityId entity, DateTime scheduledTimeUtc, string operationName, object operationInput = null);

        /// <summary>
        /// Signals an entity to perform an operation.
        /// </summary>
        /// <param name="entityKey">The target entity key.</param>
        /// <param name="operation">A delegate that performs the desired operation on the entity.</param>
        /// <typeparam name="TEntityInterface">Entity interface.</typeparam>
        void SignalEntity<TEntityInterface>(string entityKey, Action<TEntityInterface> operation);

        /// <summary>
        /// Signals an entity to perform an operation, at a specified time.
        /// </summary>
        /// <param name="entityKey">The target entity key.</param>
        /// <param name="scheduledTimeUtc">The time at which to start the operation.</param>
        /// <param name="operation">A delegate that performs the desired operation on the entity.</param>
        /// <typeparam name="TEntityInterface">Entity interface.</typeparam>
        void SignalEntity<TEntityInterface>(string entityKey, DateTime scheduledTimeUtc, Action<TEntityInterface> operation);

        /// <summary>
        /// Signals an entity to perform an operation.
        /// </summary>
        /// <param name="entityId">The target entity.</param>
        /// <param name="operation">A delegate that performs the desired operation on the entity.</param>
        /// <typeparam name="TEntityInterface">Entity interface.</typeparam>
        void SignalEntity<TEntityInterface>(EntityId entityId, Action<TEntityInterface> operation);

        /// <summary>
        /// Signals an entity to perform an operation, at a specified time.
        /// </summary>
        /// <param name="entityId">The target entity.</param>
        /// <param name="scheduledTimeUtc">The time at which to start the operation.</param>
        /// <param name="operation">A delegate that performs the desired operation on the entity.</param>
        /// <typeparam name="TEntityInterface">Entity interface.</typeparam>
        void SignalEntity<TEntityInterface>(EntityId entityId, DateTime scheduledTimeUtc, Action<TEntityInterface> operation);

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