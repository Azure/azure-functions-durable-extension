// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using DurableTask.AzureStorage;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class AzureStorageDurabilityProviderFactory : IDurabilityProviderFactory
    {
        private readonly DurableTaskOptions options;
        private readonly AzureStorageOptions azureStorageOptions;
        private readonly IConnectionStringResolver connectionStringResolver;
        private readonly string defaultConnectionName;
        private readonly INameResolver nameResolver;
        private AzureStorageDurabilityProvider defaultStorageProvider;

        // Must wait to get settings until we have validated taskhub name.
        private bool hasValidatedOptions;
        private AzureStorageOrchestrationServiceSettings defaultSettings;

        public AzureStorageDurabilityProviderFactory(
            IOptions<DurableTaskOptions> options,
            IConnectionStringResolver connectionStringResolver,
            INameResolver nameResolver)
        {
            this.options = options.Value;
            this.nameResolver = nameResolver;
            this.azureStorageOptions = new AzureStorageOptions();
            JsonConvert.PopulateObject(JsonConvert.SerializeObject(this.options.StorageProvider), this.azureStorageOptions);

            this.azureStorageOptions.Validate();

            this.connectionStringResolver = connectionStringResolver ?? throw new ArgumentNullException(nameof(connectionStringResolver));
            this.defaultConnectionName = this.azureStorageOptions.ConnectionStringName ?? ConnectionStringNames.Storage;
        }

        internal string GetDefaultStorageConnectionString()
        {
            return this.connectionStringResolver.Resolve(this.defaultConnectionName);
        }

        // This method should not be called before the app settings are resolved into the options.
        // Because of this, we wait to validate the options until right before building a durability provider, rather
        // than in the Factory constructor.
        private void EnsureInitialized()
        {
            if (!this.hasValidatedOptions)
            {
                if (!this.options.IsDefaultHubName())
                {
                    this.azureStorageOptions.ValidateHubName(this.options.HubName);
                }
                else if (!this.azureStorageOptions.IsSanitizedHubName(this.options.HubName, out string sanitizedHubName))
                {
                    this.options.SetDefaultHubName(sanitizedHubName);
                }

                this.defaultSettings = this.GetAzureStorageOrchestrationServiceSettings();
                this.hasValidatedOptions = true;
            }
        }

        public DurabilityProvider GetDurabilityProvider()
        {
            this.EnsureInitialized();
            if (this.defaultStorageProvider == null)
            {
                var defaultService = new AzureStorageOrchestrationService(this.defaultSettings);
                this.defaultStorageProvider = new AzureStorageDurabilityProvider(
                    defaultService,
                    this.defaultConnectionName,
                    this.azureStorageOptions);
            }

            return this.defaultStorageProvider;
        }

        public DurabilityProvider GetDurabilityProvider(DurableClientAttribute attribute)
        {
            this.EnsureInitialized();
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
                innerClient = new AzureStorageDurabilityProvider(
                    new AzureStorageOrchestrationService(settings),
                    connectionName,
                    this.azureStorageOptions);
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
                ThrowExceptionOnInvalidDedupeStatus = true,
            };

            // When running on App Service VMSS stamps, these environment variables are the best way
            // to enure unqique worker names
            string stamp = this.nameResolver.Resolve("WEBSITE_CURRENT_STAMPNAME");
            string roleInstance = this.nameResolver.Resolve("RoleInstanceId");
            if (!string.IsNullOrEmpty(stamp) && !string.IsNullOrEmpty(roleInstance))
            {
                settings.WorkerId = $"{stamp}:{roleInstance}";
            }

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
