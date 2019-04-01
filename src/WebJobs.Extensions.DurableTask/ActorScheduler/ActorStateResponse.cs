// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// The response returned by <see cref="IDurableOrchestrationClient.ReadActorState"/>.
    /// </summary>
    /// <typeparam name="T">The JSON-serializable type of the actor.</typeparam>
    public struct ActorStateResponse<T>
    {
        /// <summary>
        /// Whether this actor exists or not.
        /// </summary>
        public bool ActorExists { get; set; }

        /// <summary>
        /// The current state of the actor, if it exists, or default(<typeparamref name="T"/>) otherwise.
        /// </summary>
        public T ActorState { get; set; }
    }
}
