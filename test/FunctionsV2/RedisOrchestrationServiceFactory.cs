// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DurableTask.Redis;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class RedisOrchestrationServiceFactory : IDurabilityProviderFactory
    {
        private readonly DurabilityProvider defaultProvider;
        private readonly string redisConnectionString;
        private readonly string defaultHubName;

        public RedisOrchestrationServiceFactory(IOptions<DurableTaskCustomStorageOptions> options, IConnectionStringResolver connectionStringResolver, string connectionStringName)
        {
            this.redisConnectionString = connectionStringResolver.Resolve(connectionStringName);
            this.defaultHubName = options.Value.HubName;
            var defaultTaskHubService = new RedisOrchestrationService(new RedisOrchestrationServiceSettings()
            {
                TaskHubName = this.defaultHubName,
                RedisConnectionString = this.redisConnectionString,
            });

            this.defaultProvider = new DurabilityProvider("Redis", defaultTaskHubService, defaultTaskHubService);
        }

        public bool SupportsEntities => false;

        public DurabilityProvider GetDurabilityProvider(DurableClientAttribute attribute)
        {
            if (string.IsNullOrEmpty(attribute.TaskHub) || string.Equals(attribute.TaskHub, this.defaultHubName))
            {
                return this.defaultProvider;
            }

            var redisOrchestartionService = new RedisOrchestrationService(new RedisOrchestrationServiceSettings()
            {
                TaskHubName = attribute.TaskHub,
                RedisConnectionString = this.redisConnectionString,
            });

            return new DurabilityProvider("Redis", redisOrchestartionService, redisOrchestartionService);
        }

        public DurabilityProvider GetDurabilityProvider()
        {
            return this.defaultProvider;
        }
    }
}
