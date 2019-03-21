// Copyright (c) .NET Foundation. All rights reserved.
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
    public interface IDurableActorContext : IDeterministicExecutionContext
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
        string OperationName { get; }

        /// <summary>
        /// A logger for logging information about this actor.
        /// </summary>
        ILogger Logger { get; }

        /// <summary>
        /// Whether this actor is freshly constructed, i.e. did not exist prior to this operation being called.
        /// </summary>
        bool NewlyConstructed { get; }

        /// <summary>
        /// Gets a typed view of the state, by deserializing the JSON.
        /// </summary>
        /// <typeparam name="TState">the JSON-serializable type of the actor state.</typeparam>
        /// <returns>a typed view that allows reading and updating.</returns>
        IStateView<TState> GetStateAs<TState>(Formatting formatting = Formatting.Indented, JsonSerializerSettings settings = null);

        /// <summary>
        /// Gets the argument that was passed to this operation as a deserialized value.
        /// </summary>
        /// <typeparam name="TArgument">The JSON-serializable type used for the operation argument.</typeparam>
        /// <returns>the value of the argument, or default(<typeparamref name="TArgument"/>) if none was given.</returns>
        TArgument GetArgument<TArgument>();

        /// <summary>
        /// Gets the argument that was passed to this operation as a deserialized value.
        /// </summary>
        /// <param name="argumentType">The JSON-serializable type used for the operation argument.</param>
        /// <returns>the value of the argument, or default(<paramref name="argumentType"/>) if none was given.</returns>
        object GetArgument(Type argumentType);

        /// <summary>
        /// Returns the given result to the caller of this operation. 
        /// </summary>
        /// <typeparam name="TResult">the type of the result.</typeparam>
        /// <param name="result">the result to return.</param>
        void Return<TResult>(TResult result);

        /// <summary>
        /// Returns the given result to the caller of this operation. 
        /// </summary>
        /// <param name="result">the result to return.</param>
        /// <param name="resultType">optionally, the type of the result.</param>
        void Return(object result, Type resultType = null);

        /// <summary>
        /// Delete this actor after this operation completes.
        /// </summary>
        void DestructOnExit();
    }

    /// <summary>
    /// A typed view of the current state of the actor.
    /// </summary>
    /// <typeparam name="TState">The JSON-serializable type used for this actor.</typeparam>
    public interface IStateView<TState> : IStateView
    {
        /// <summary>
        /// The current state of the actor.
        /// </summary>
        TState Value { get; set; }
    }

    /// <summary>
    /// A view of the current state of the actor.
    /// </summary>
    public interface IStateView : IDisposable
    {
        /// <summary>
        /// Serializes the current state to JSON.
        /// </summary>
        void WriteBack();
    }

}