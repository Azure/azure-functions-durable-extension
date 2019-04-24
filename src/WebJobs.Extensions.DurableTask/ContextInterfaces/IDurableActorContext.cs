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
    /// Provides functionality for application code implementing an actor operation.
    /// </summary>
    public interface IDurableActorContext
    {
        /// <summary>
        /// Gets the class of the currently executing actor.
        /// </summary>
        string ActorClass { get; }

        /// <summary>
        /// Gets the key of the currently executing actor.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Gets an actor reference for the currently executing actor.
        /// </summary>
        ActorId Self { get; }

        /// <summary>
        /// Gets the name of the operation that was called.
        /// </summary>
        /// <remarks>
        /// An operation invocation on an actor includes an operation name, which states what
        /// operation to perform, and optionally an operation content, which
        /// provides an input argument to the operation.
        /// </remarks>
        string OperationName { get; }

        /// <summary>
        /// Whether this actor is freshly constructed, i.e. did not exist prior to this operation being called.
        /// </summary>
        bool IsNewlyConstructed { get; }

        /// <summary>
        /// Gets a typed view of the state, by deserializing the JSON.
        /// </summary>
        /// <typeparam name="TState">The JSON-serializable type of the actor state.</typeparam>
        /// <returns>A typed view that allows reading and updating.</returns>
        IStateView<TState> GetState<TState>(Formatting formatting = Formatting.Indented, JsonSerializerSettings settings = null);

        /// <summary>
        /// Gets the content (operation input) that was passed passed along when this operation was called, as a deserialized value.
        /// </summary>
        /// <typeparam name="T">The JSON-serializable type used for the operation content.</typeparam>
        /// <returns>The operation content, or default(<typeparamref name="T"/>) if none.</returns>
        /// <remarks>
        /// An operation invocation on an actor includes an operation name, which states what
        /// operation to perform, and optionally an operation content, which
        /// provides an input argument to the operation.
        /// </remarks>
        T GetOperationContent<T>();

        /// <summary>
        /// Gets the content (operation input) that was passed passed along when this operation was called, as a deserialized value.
        /// </summary>
        /// <param name="contentType">The JSON-serializable type used for the operation content.</param>
        /// <returns>The operation content, or default(<paramref name="contentType"/>) if none.</returns>
        /// <remarks>
        /// An operation invocation on an actor includes an operation name, which states what
        /// operation to perform, and optionally an operation content, which
        /// provides an input argument to the operation.
        /// </remarks>
        object GetOperationContent(Type contentType);

         /// <summary>
        /// Returns the given result to the caller of this operation.
        /// </summary>
        /// <param name="result">the result to return.</param>
        void Return(object result);

        /// <summary>
        /// Deletes this actor after this operation completes.
        /// </summary>
        void DestructOnExit();

        /// <summary>
        /// Signals an actor to perform an operation, without waiting for a response. Any result or exception is ignored (fire and forget).
        /// </summary>
        /// <param name="actor">The target actor.</param>
        /// <param name="operationName">The name of the operation.</param>
        /// <param name="operationContent">The content for the operation.</param>
        void SignalActor(ActorId actor, string operationName, object operationContent = null);
    }
}