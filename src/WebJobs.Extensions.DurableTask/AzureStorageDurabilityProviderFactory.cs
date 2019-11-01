// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using DurableTask.AzureStorage;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class AzureStorageDurabilityProviderFactory : IDurabilityProviderFactory
    {
        private readonly DurableTaskOptions options;
        private readonly AzureStorageOptions azureStorageOptions;
        private readonly IConnectionStringResolver connectionStringResolver;
        private readonly AzureStorageOrchestrationServiceSettings defaultSettings;
        private readonly string defaultConnectionName;
        private AzureStorageDurabilityProvider defaultStorageProvider;

        public AzureStorageDurabilityProviderFactory(
            IOptions<DurableTaskOptions> options,
            IConnectionStringResolver connectionStringResolver)
        {
            this.options = options.Value;
            this.azureStorageOptions = new AzureStorageOptions();
            JsonConvert.PopulateObject(JsonConvert.SerializeObject(this.options.StorageProvider), this.azureStorageOptions);

            this.azureStorageOptions.Validate();
            if (!this.options.IsDefaultHubName())
            {
                this.azureStorageOptions.ValidateHubName(this.options.HubName);
            }
            else if (!this.azureStorageOptions.IsSanitizedHubName(this.options.HubName, out string sanitizedHubName))
            {
                this.options.HubName = sanitizedHubName;
            }

            this.connectionStringResolver = connectionStringResolver;
            this.defaultConnectionName = this.azureStorageOptions.ConnectionStringName ?? ConnectionStringNames.Storage;
            this.defaultSettings = this.GetAzureStorageOrchestrationServiceSettings();
        }

        public DurabilityProvider GetDurabilityProvider()
        {
            if (this.defaultStorageProvider == null)
            {
                var defaultService = new AzureStorageOrchestrationService(this.defaultSettings);
                this.defaultStorageProvider = new AzureStorageDurabilityProvider(defaultService, this.defaultConnectionName);
            }

            return this.defaultStorageProvider;
        }

        public DurabilityProvider GetDurabilityProvider(DurableClientAttribute attribute)
        {
            return this.GetAzureStorageStorageProvider(attribute);
        }

        private AzureStorageDurabilityProvider GetAzureStorageStorageProvider(DurableClientAttribute attribute)
        {
            string connectionName = attribute.ConnectionName ?? this.defaultConnectionName;
            AzureStorageOrchestrationServiceSettings settings = this.GetAzureStorageOrchestrationServiceSettings(connectionName, attribute.TaskHub);

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
                innerClient = new AzureStorageDurabilityProvider(new AzureStorageOrchestrationService(settings), connectionName);
            }

            return innerClient;
        }

        internal AzureStorageOrchestrationServiceSettings GetAzureStorageOrchestrationServiceSettings(
            string connectionName = null,
            string taskHubNameOverride = null)
        {
            connectionName = connectionName ?? this.defaultConnectionName;

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
                TaskHubName = taskHubNameOverride ?? this.options.HubName,
                PartitionCount = this.azureStorageOptions.PartitionCount,
                ControlQueueBatchSize = this.azureStorageOptions.ControlQueueBatchSize,
                ControlQueueBufferThreshold = this.azureStorageOptions.ControlQueueBufferThreshold,
                ControlQueueVisibilityTimeout = this.azureStorageOptions.ControlQueueVisibilityTimeout,
                WorkItemQueueVisibilityTimeout = this.azureStorageOptions.WorkItemQueueVisibilityTimeout,
                MaxConcurrentTaskOrchestrationWorkItems = this.options.MaxConcurrentOrchestratorFunctions,
                MaxConcurrentTaskActivityWorkItems = this.options.MaxConcurrentActivityFunctions,
                ExtendedSessionsEnabled = this.options.ExtendedSessionsEnabled,
                ExtendedSessionIdleTimeout = extendedSessionTimeout,
                MaxQueuePollingInterval = this.azureStorageOptions.MaxQueuePollingInterval,
                TrackingStoreStorageAccountDetails = GetStorageAccountDetailsOrNull(
                    this.connectionStringResolver,
                    this.azureStorageOptions.TrackingStoreConnectionStringName),
                FetchLargeMessageDataEnabled = this.azureStorageOptions.FetchLargeMessagesAutomatically,
            };

            if (!string.IsNullOrEmpty(this.azureStorageOptions.TrackingStoreNamePrefix))
            {
                settings.TrackingStoreNamePrefix = this.azureStorageOptions.TrackingStoreNamePrefix;
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
