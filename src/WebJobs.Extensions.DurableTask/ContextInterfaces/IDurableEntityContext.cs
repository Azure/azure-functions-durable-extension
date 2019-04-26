﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs
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
        string Key { get; }

        /// <summary>
        /// Gets an entity id for the currently executing entity.
        /// </summary>
        EntityId Self { get; }

        /// <summary>
        /// Gets the name of the operation that was called.
        /// </summary>
        /// <remarks>
        /// An operation invocation on an entity includes an operation name, which states what
        /// operation to perform, and optionally an operation input.
        /// </remarks>
        string OperationName { get; }

        /// <summary>
        /// Whether this entity is freshly constructed, i.e. did not exist prior to this operation being called.
        /// </summary>
        bool IsNewlyConstructed { get; }

        /// <summary>
        /// Gets the current state of this entity, for reading and/or updating.
        /// </summary>
        /// <typeparam name="TState">The JSON-serializable type of the entity state.</typeparam>
        /// <returns>The current state of this entity.</returns>
        /// <exception cref="InvalidCastException">If the current state has an incompatible type.</exception>
        TState GetState<TState>();

        /// <summary>
        /// Sets the current state of this entity.
        /// </summary>
        /// <param name="state">The JSON-serializable state of the entity.</param>
        void SetState(object state);

        /// <summary>
        /// Gets the input for this operation, as a deserialized value.
        /// </summary>
        /// <typeparam name="T">The JSON-serializable type used for the operation input.</typeparam>
        /// <returns>The operation input, or default(<typeparamref name="T"/>) if none.</returns>
        /// <remarks>
        /// An operation invocation on an entity includes an operation name, which states what
        /// operation to perform, and optionally an operation input.
        /// </remarks>
        T GetInput<T>();

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
        /// Deletes this entity after this operation completes.
        /// </summary>
        void DestructOnExit();

        /// <summary>
        /// Signals an entity to perform an operation, without waiting for a response. Any result or exception is ignored (fire and forget).
        /// </summary>
        /// <param name="entity">The target entity.</param>
        /// <param name="operationName">The name of the operation.</param>
        /// <param name="operationInput">The operation input.</param>
        void SignalEntity(EntityId entity, string operationName, object operationInput = null);
    }
}