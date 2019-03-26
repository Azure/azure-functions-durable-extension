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
    /// A unique identifier for an actor, consisting of actor class and actor key.
    /// </summary>
    public struct ActorId : IEquatable<ActorId>, IComparable
    {
        /// <summary>
        /// Create an actor id for an actor.
        /// </summary>
        /// <param name="actorClass">The name of the actor class.</param>
        /// <param name="actorKey">The actor key.</param>
        public ActorId(string actorClass, string actorKey)
        {
            if (string.IsNullOrEmpty(actorClass))
            {
                throw new ArgumentNullException(nameof(actorClass), "Invalid actor reference: actor class must not be a null or empty string.");
            }

            this.ActorClass = actorClass;
            this.ActorKey = actorKey ?? throw new ArgumentNullException(nameof(actorKey), "Invalid actor reference: actor key must not be null.");
        }

        /// <summary>
        /// The name of the actor class.
        /// </summary>
        [JsonProperty("class")]
        public string ActorClass { get; }

        /// <summary>
        /// The actor key. Uniquely identifies an actor among all instances of the same class.
        /// </summary>
        [JsonProperty("key")]
        public string ActorKey { get; }

        internal static string GetSchedulerIdFromActorId(ActorId actorId)
        {
            return $"@{actorId.ActorClass}@{actorId.ActorKey}";
        }

        internal static ActorId GetActorIdFromSchedulerId(string schedulerId)
        {
            var pos = schedulerId.IndexOf('@', 1);
            var actorClass = schedulerId.Substring(1, pos - 1);
            var actorKey = schedulerId.Substring(pos + 1);
            return new ActorId(actorClass, actorKey);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return GetSchedulerIdFromActorId(this);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return (obj is ActorId other) && this.Equals(other);
        }

        /// <inheritdoc/>
        public bool Equals(ActorId other)
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
            var other = (ActorId)obj;
            return ((IComparable)(this.ActorKey, this.ActorClass))
                      .CompareTo((other.ActorKey, other.ActorClass));
        }
    }
}
