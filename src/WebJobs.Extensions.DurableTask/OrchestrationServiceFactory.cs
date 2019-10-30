// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using DurableTask.AzureStorage;
using DurableTask.Core;
using DurableTask.Emulator;
using DurableTask.Redis;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class OrchestrationServiceFactory : IOrchestrationServiceFactory
    {
        private Lazy<IOrchestrationServiceFactory> innerFactory;

        public OrchestrationServiceFactory(
            IOptions<DurableTaskOptions> options,
            IConnectionStringResolver connectionStringResolver)
        {
            this.innerFactory = new Lazy<IOrchestrationServiceFactory>(() => GetInnerFactory(options.Value, connectionStringResolver));
        }

        private static IOrchestrationServiceFactory GetInnerFactory(DurableTaskOptions options, IConnectionStringResolver connectionStringResolver)
        {
            var configuredProvider = options.StorageProvider.GetConfiguredProvider();
            switch (configuredProvider)
            {
                case AzureStorageOptions azureStorageOptions:
                    return new AzureStorageOrchestrationServiceFactory(options, connectionStringResolver);
                case EmulatorStorageOptions emulatorStorageOptions:
                    return new EmulaterOrchestrationServiceFactory(options);
                case RedisStorageOptions redisStorageOptions:
                    return new RedisOrchestrationServiceFactory(options, connectionStringResolver);
                default:
                    throw new InvalidOperationException($"{configuredProvider.GetType()} is not a supported storage provider.");
            }
        }

        public IOrchestrationService GetOrchestrationService()
        {
            return this.innerFactory.Value.GetOrchestrationService();
        }

        public IOrchestrationServiceClient GetOrchestrationClient(DurableClientAttribute attribute)
        {
            return this.innerFactory.Value.GetOrchestrationClient(attribute);
        }

        private static StorageAccountDetails GetStorageAccountDetailsOrNull(IConnectionStringResolver connectionStringResolver, string connectionName)
        {
            if (string.IsNullOrEmpty(connectionName))
            {
                return null;
            }

            string resolvedStorageConnectionString = connectionStringResolver.Resolve(connectionName);
            if (string.IsNullOrEmpty(resolvedStorageConnectionString))
            {
                throw new InvalidOperationException($"Unable to resolve the Azure Storage connection named '{connectionName}'.");
            }

            return new StorageAccountDetails
            {
                ConnectionString = resolvedStorageConnectionString,
            };
        }

        private class AzureStorageOrchestrationServiceFactory : IOrchestrationServiceFactory
        {
            private readonly DurableTaskOptions options;
            private readonly IConnectionStringResolver connectionStringResolver;
            private readonly AzureStorageOrchestrationServiceSettings defaultSettings;
            private AzureStorageOrchestrationService defaultService;

            public AzureStorageOrchestrationServiceFactory(
                DurableTaskOptions options,
                IConnectionStringResolver connectionStringResolver)
            {
                this.options = options;
                this.connectionStringResolver = connectionStringResolver;
                this.defaultSettings = this.GetAzureStorageOrchestrationServiceSettings(options);
            }

            public IOrchestrationService GetOrchestrationService()
            {
                if (this.defaultService == null)
                {
                    this.defaultService = new AzureStorageOrchestrationService(this.defaultSettings);
                }

                return this.defaultService;
            }

            public IOrchestrationServiceClient GetOrchestrationClient(DurableClientAttribute attribute)
            {
                return this.GetAzureStorageOrchestrationService(attribute);
            }

            private AzureStorageOrchestrationService GetAzureStorageOrchestrationService(DurableClientAttribute attribute)
            {
                AzureStorageOrchestrationServiceSettings settings = this.GetOrchestrationServiceSettings(attribute);

                AzureStorageOrchestrationService innerClient;
                if (string.Equals(this.defaultSettings.TaskHubName, settings.TaskHubName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(this.defaultSettings.StorageConnectionString, settings.StorageConnectionString, StringComparison.OrdinalIgnoreCase))
                {
                    // It's important that clients use the same AzureStorageOrchestrationService instance
                    // as the host when possible to ensure we any send operations can be picked up
                    // immediately instead of waiting for the next queue polling interval.
                    innerClient = this.defaultService;
                }
                else
                {
                    innerClient = new AzureStorageOrchestrationService(settings);
                }

                return innerClient;
            }

            internal AzureStorageOrchestrationServiceSettings GetOrchestrationServiceSettings(DurableClientAttribute attribute)
            {
                return this.GetAzureStorageOrchestrationServiceSettings(
                    this.options,
                    connectionNameOverride: attribute.ConnectionName,
                    taskHubNameOverride: attribute.TaskHub);
            }

            internal AzureStorageOrchestrationServiceSettings GetAzureStorageOrchestrationServiceSettings(
                DurableTaskOptions durableTaskOptions,
                string connectionNameOverride = null,
                string taskHubNameOverride = null)
            {
                var azureStorageOptions = durableTaskOptions.StorageProvider.AzureStorage;
                string connectionName = connectionNameOverride ?? azureStorageOptions.ConnectionStringName ?? ConnectionStringNames.Storage;
                string resolvedStorageConnectionString = this.connectionStringResolver.Resolve(connectionName);

                if (string.IsNullOrEmpty(resolvedStorageConnectionString))
                {
                    throw new InvalidOperationException("Unable to find an Azure Storage connection string to use for this binding.");
                }

                TimeSpan extendedSessionTimeout = TimeSpan.FromSeconds(
                    Math.Max(this.options.ExtendedSessionIdleTimeoutInSeconds, 0));

                var settings = new AzureStorageOrchestrationServiceSettings
                {
                    StorageConnectionString = resolvedStorageConnectionString,
                    TaskHubName = taskHubNameOverride ?? durableTaskOptions.HubName,
                    PartitionCount = azureStorageOptions.PartitionCount,
                    ControlQueueBatchSize = azureStorageOptions.ControlQueueBatchSize,
                    ControlQueueVisibilityTimeout = azureStorageOptions.ControlQueueVisibilityTimeout,
                    WorkItemQueueVisibilityTimeout = azureStorageOptions.WorkItemQueueVisibilityTimeout,
                    MaxConcurrentTaskOrchestrationWorkItems = this.options.MaxConcurrentOrchestratorFunctions,
                    MaxConcurrentTaskActivityWorkItems = this.options.MaxConcurrentActivityFunctions,
                    ExtendedSessionsEnabled = this.options.ExtendedSessionsEnabled,
                    ExtendedSessionIdleTimeout = extendedSessionTimeout,
                    MaxQueuePollingInterval = azureStorageOptions.MaxQueuePollingInterval,
                    TrackingStoreStorageAccountDetails = GetStorageAccountDetailsOrNull(
                        this.connectionStringResolver,
                        azureStorageOptions.TrackingStoreConnectionStringName),
                    FetchLargeMessageDataEnabled = azureStorageOptions.FetchLargeMessagesAutomatically,
                };

                if (!string.IsNullOrEmpty(azureStorageOptions.TrackingStoreNamePrefix))
                {
                    settings.TrackingStoreNamePrefix = azureStorageOptions.TrackingStoreNamePrefix;
                }

                return settings;
            }
        }

        private class EmulaterOrchestrationServiceFactory : IOrchestrationServiceFactory
        {
            private readonly LocalOrchestrationService service;

            public EmulaterOrchestrationServiceFactory(DurableTaskOptions options)
            {
                this.service = new LocalOrchestrationService();
            }

            public IOrchestrationServiceClient GetOrchestrationClient(DurableClientAttribute attribute)
            {
                return (IOrchestrationServiceClient)this.service;
            }

            public IOrchestrationService GetOrchestrationService()
            {
                return (IOrchestrationService)this.service;
            }
        }

        private class RedisOrchestrationServiceFactory : IOrchestrationServiceFactory
        {
            private readonly RedisOrchestrationService defaultTaskHubService;
            private readonly string redisConnectionString;
            private readonly string defaultHubName;

            public RedisOrchestrationServiceFactory(DurableTaskOptions options, IConnectionStringResolver connectionStringResolver)
            {
                this.redisConnectionString = connectionStringResolver.Resolve(options.StorageProvider.Redis.ConnectionStringName);
                this.defaultHubName = options.HubName;
                this.defaultTaskHubService = new RedisOrchestrationService(new RedisOrchestrationServiceSettings()
                {
                    TaskHubName = this.defaultHubName,
                    RedisConnectionString = this.redisConnectionString,
                });
            }

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
        }
    }
}
