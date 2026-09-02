// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal readonly struct DurableClientCacheKey : IEquatable<DurableClientCacheKey>
    {
        private readonly string taskHub;
        private readonly string connectionName;
        private readonly bool externalClient;
        private readonly bool durableRequiresGrpc;

        public DurableClientCacheKey(DurableClientAttribute attribute)
        {
            this.taskHub = attribute.TaskHub;
            this.connectionName = attribute.ConnectionName;
            this.externalClient = attribute.ExternalClient;
            this.durableRequiresGrpc = attribute.DurableRequiresGrpc;
        }

        public bool Equals(DurableClientCacheKey other)
        {
            return string.Equals(this.taskHub, other.taskHub, StringComparison.Ordinal)
                && string.Equals(this.connectionName, other.connectionName, StringComparison.Ordinal)
                && this.externalClient == other.externalClient
                && this.durableRequiresGrpc == other.durableRequiresGrpc;
        }

        public override bool Equals(object obj)
        {
            return obj is DurableClientCacheKey other && this.Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 17;
                hashCode = (hashCode * 31) + (this.taskHub == null
                    ? 0
                    : StringComparer.Ordinal.GetHashCode(this.taskHub));
                hashCode = (hashCode * 31) + (this.connectionName == null
                    ? 0
                    : StringComparer.Ordinal.GetHashCode(this.connectionName));
                hashCode = (hashCode * 31) + this.externalClient.GetHashCode();
                hashCode = (hashCode * 31) + this.durableRequiresGrpc.GetHashCode();
                return hashCode;
            }
        }
    }
}
