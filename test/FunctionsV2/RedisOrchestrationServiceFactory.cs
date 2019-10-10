// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DurableTask.Core;
using DurableTask.Redis;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.ContextImplementations;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class RedisOrchestrationServiceFactory : IOrchestrationServiceFactory
    {
        private readonly RedisOrchestrationService defaultTaskHubService;
        private readonly string redisConnectionString;
        private readonly string defaultHubName;

        public RedisOrchestrationServiceFactory(IOptions<DurableTaskCustomStorageOptions> options, IConnectionStringResolver connectionStringResolver, string connectionStringName)
        {
            this.redisConnectionString = connectionStringResolver.Resolve(connectionStringName);
            this.defaultHubName = options.Value.HubName;
            this.defaultTaskHubService = new RedisOrchestrationService(new RedisOrchestrationServiceSettings()
            {
                TaskHubName = this.defaultHubName,
                RedisConnectionString = this.redisConnectionString,
            });
        }

        public bool SupportsEntities => false;

        public IOrchestrationServiceClient GetOrchestrationClient(DurableClientAttribute attribute)
        {
            if (string.IsNullOrEmpty(attribute.TaskHub) || string.Equals(attribute.TaskHub, this.defaultHubName))
            {
                return this.defaultTaskHubService;
            }

            return new RedisOrchestrationService(new RedisOrchestrationServiceSettings()
            {
                TaskHubName = attribute.TaskHub,
                RedisConnectionString = this.redisConnectionString,
            });
        }

        public IOrchestrationService GetOrchestrationService()
        {
            return this.defaultTaskHubService;
        }

        public IDurableSpecialOperationsClient GetSpecialtyClient(TaskHubClient client)
        {
            return new DefaultDurableSpecialOperationsClient("Redis");
        }
    }
}
