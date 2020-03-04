// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// A unique identifier for an entity, consisting of entity name and entity key.
    /// </summary>
    public struct EntityId : IEquatable<EntityId>, IComparable
    {
        private string schedulerId;

        /// <summary>
        /// Create an entity id for an entity.
        /// </summary>
        /// <param name="entityName">The name of this class of entities.</param>
        /// <param name="entityKey">The entity key.</param>
        public EntityId(string entityName, string entityKey)
        {
            if (string.IsNullOrEmpty(entityName))
            {
                throw new ArgumentNullException(nameof(entityName), "Invalid entity id: entity name must not be a null or empty string.");
            }

            this.EntityName = entityName.ToLowerInvariant();
            this.EntityKey = entityKey ?? throw new ArgumentNullException(nameof(entityKey), "Invalid entity id: entity key must not be null.");
            this.schedulerId = GetSchedulerId(this.EntityName, this.EntityKey);
        }

        /// <summary>
        /// The name for this class of entities.
        /// </summary>
        [JsonProperty(PropertyName = "name", Required = Required.Always)]
        public string EntityName { get; private set; } // do not remove set, is needed by Json Deserializer

        /// <summary>
        /// The entity key. Uniquely identifies an entity among all entities of the same name.
        /// </summary>
        [JsonProperty(PropertyName = "key", Required = Required.Always)]
        public string EntityKey { get; private set; } // do not remove set, is needed by Json Deserializer

        internal static string GetSchedulerIdFromEntityId(EntityId entityId)
        {
            return GetSchedulerId(entityId.EntityName, entityId.EntityKey);
        }

        private static string GetSchedulerId(string entityName, string entityKey)
        {
            return $"@{entityName}@{entityKey}";
        }

        internal static string GetSchedulerIdPrefixFromEntityName(string entityName)
        {
            return $"@{entityName.ToLowerInvariant()}@";
        }

        internal static EntityId GetEntityIdFromSchedulerId(string schedulerId)
        {
            var pos = schedulerId.IndexOf('@', 1);
            var entityName = schedulerId.Substring(1, pos - 1);
            var entityKey = schedulerId.Substring(pos + 1);
            return new EntityId(entityName, entityKey);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            // The scheduler id could be null if the object was deserialized.
            if (this.schedulerId == null)
            {
                this.schedulerId = GetSchedulerIdFromEntityId(this);
            }

            return this.schedulerId;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return (obj is EntityId other) && this.Equals(other);
        }

        /// <inheritdoc/>
        public bool Equals(EntityId other)
        {
            return this.ToString().Equals(other.ToString());
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return this.ToString().GetHashCode();
        }

        /// <inheritdoc/>
        public int CompareTo(object obj)
        {
            var other = (EntityId)obj;
            return this.ToString().CompareTo(other.ToString());
        }
    }
}
