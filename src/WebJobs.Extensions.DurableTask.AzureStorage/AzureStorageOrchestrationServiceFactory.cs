// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using DurableTask.AzureStorage;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Azure
{
    internal class AzureStorageOrchestrationServiceFactory : IDurabilityProviderFactory
    {
        private readonly DurableTaskAzureStorageOptions options;
        private readonly IConnectionStringResolver connectionStringResolver;
        private readonly AzureStorageOrchestrationServiceSettings defaultSettings;
        private AzureStorageDurabilityProvider defaultStorageProvider;

        public AzureStorageOrchestrationServiceFactory(
            IOptions<DurableTaskAzureStorageOptions> options,
            IConnectionStringResolver connectionStringResolver)
        {
            this.options = options.Value;
            this.connectionStringResolver = connectionStringResolver;
            this.defaultSettings = this.GetAzureStorageOrchestrationServiceSettings(this.options);
        }

        public DurabilityProvider GetDurabilityProvider()
        {
            if (this.defaultStorageProvider == null)
            {
                var defaultService = new AzureStorageOrchestrationService(this.defaultSettings);
                this.defaultStorageProvider = new AzureStorageDurabilityProvider(defaultService);
            }

            return this.defaultStorageProvider;
        }

        public DurabilityProvider GetDurabilityProvider(DurableClientAttribute attribute)
        {
            return this.GetAzureStorageStorageProvider(attribute);
        }

        private AzureStorageDurabilityProvider GetAzureStorageStorageProvider(DurableClientAttribute attribute)
        {
            AzureStorageOrchestrationServiceSettings settings = this.GetOrchestrationServiceSettings(attribute);

            AzureStorageDurabilityProvider innerClient;
            if (string.Equals(this.defaultSettings.TaskHubName, settings.TaskHubName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(this.defaultSettings.StorageConnectionString, settings.StorageConnectionString, StringComparison.OrdinalIgnoreCase))
            {
                // It's important that clients use the same AzureStorageOrchestrationService instance
                // as the host when possible to ensure we any send operations can be picked up
                // immediately instead of waiting for the next queue polling interval.
                innerClient = this.defaultStorageProvider;
            }
            else
            {
                innerClient = new AzureStorageDurabilityProvider(new AzureStorageOrchestrationService(settings));
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
            DurableTaskAzureStorageOptions durableTaskOptions,
            string connectionNameOverride = null,
            string taskHubNameOverride = null)
        {
            var azureStorageOptions = durableTaskOptions.AzureStorageProvider;
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
    }
 }
