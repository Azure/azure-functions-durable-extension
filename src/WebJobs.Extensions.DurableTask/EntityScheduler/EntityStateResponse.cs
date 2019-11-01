// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// The response returned by <see cref="IDurableEntityClient.ReadEntityStateAsync"/>.
    /// </summary>
    /// <typeparam name="T">The JSON-serializable type of the entity.</typeparam>
    public struct EntityStateResponse<T>
    {
        /// <summary>
        /// Whether this entity exists or not.
        /// </summary>
        public bool EntityExists { get; set; }

        /// <summary>
        /// The current state of the entity, if it exists, or default(<typeparamref name="T"/>) otherwise.
        /// </summary>
        public T EntityState { get; set; }
    }
}
