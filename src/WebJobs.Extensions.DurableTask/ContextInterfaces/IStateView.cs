// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs
{
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