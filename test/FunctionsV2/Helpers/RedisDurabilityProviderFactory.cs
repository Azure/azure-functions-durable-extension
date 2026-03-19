// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DurableTask.Redis;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class RedisDurabilityProviderFactory : IDurabilityProviderFactory
    {
        private readonly string defaultConnectionName;

        private readonly DurabilityProvider defaultProvider;
        private readonly string defaultHubName;
        private readonly IConnectionInfoResolver connectionResolver;

        public RedisDurabilityProviderFactory(IOptions<DurableTaskOptions> options, IConnectionInfoResolver connectionResolver)
        {
            this.defaultConnectionName = options.Value.StorageProvider["connectionName"] as string;
            string redisConnectionString = connectionResolver.Resolve(this.defaultConnectionName).Value;
            this.defaultHubName = options.Value.HubName;
            this.connectionResolver = connectionResolver;
            var defaultTaskHubService = new RedisOrchestrationService(new RedisOrchestrationServiceSettings()
            {
                TaskHubName = this.defaultHubName,
                RedisConnectionString = redisConnectionString,
            });

            this.defaultProvider = new DurabilityProvider("Redis", defaultTaskHubService, defaultTaskHubService, this.defaultConnectionName);
        }

        public bool SupportsEntities => false;

        public string Name => "Redis";

        public DurabilityProvider GetDurabilityProvider(DurableClientAttribute attribute)
        {
            if (string.IsNullOrEmpty(attribute.TaskHub) && string.IsNullOrEmpty(attribute.ConnectionName))
            {
                return this.defaultProvider;
            }

            if (string.Equals(attribute.TaskHub, this.defaultHubName) && string.Equals(attribute.ConnectionName, this.defaultConnectionName))
            {
                return this.defaultProvider;
            }

            string redisConnectionString = this.connectionResolver.Resolve(attribute.ConnectionName).Value;
            var redisOrchestartionService = new RedisOrchestrationService(new RedisOrchestrationServiceSettings()
            {
                TaskHubName = attribute.TaskHub,
                RedisConnectionString = redisConnectionString,
            });

            return new DurabilityProvider("Redis", redisOrchestartionService, redisOrchestartionService, attribute.ConnectionName);
        }

        public DurabilityProvider GetDurabilityProvider()
        {
            return this.defaultProvider;
        }
    }
}
