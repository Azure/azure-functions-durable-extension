// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// A reference to an actor, used to identify actor instances.
    /// </summary>
    public struct ActorRef : IEquatable<ActorRef>, IComparable
    {
        /// <summary>
        /// Create an actor reference for an actor.
        /// </summary>
        /// <param name="actorClass">the name of the actor class.</param>
        /// <param name="actorKey">the actor key.</param>
        public ActorRef(string actorClass, string actorKey)
        {
            if (string.IsNullOrEmpty(actorClass))
            {
                throw new ArgumentException("invalid actor reference: actor class must not be a null or empty string", actorClass);
            }

            this.ActorClass = actorClass;
            this.ActorKey = actorKey;
        }

        /// <summary>
        /// The name of the actor class.
        /// </summary>
        [JsonProperty("class")]
        public string ActorClass { get; set; }

        /// <summary>
        /// The actor key.
        /// </summary>
        [JsonProperty("key")]
        public string ActorKey { get; set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{this.ActorClass}:{this.ActorKey}";
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return (obj is ActorRef other) && this.Equals(other);
        }

        /// <inheritdoc/>
        public bool Equals(ActorRef other)
        {
            return (this.ActorClass, this.ActorKey).Equals((other.ActorClass, other.ActorKey));
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return (this.ActorClass, this.ActorKey).GetHashCode();
        }

        /// <inheritdoc/>
        public int CompareTo(object obj)
        {
            var other = (ActorRef)obj;
            return ((IComparable)(this.ActorKey, this.ActorClass))
                      .CompareTo((other.ActorKey, other.ActorClass));
        }
    }
}
