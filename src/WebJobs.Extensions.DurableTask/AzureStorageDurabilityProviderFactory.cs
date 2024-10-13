// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using DurableTask.AzureStorage;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class AzureStorageDurabilityProviderFactory : IDurabilityProviderFactory
    {
        private const string LoggerName = "Host.Triggers.DurableTask.AzureStorage";
        internal const string ProviderName = "AzureStorage";

        private readonly DurableTaskOptions options;
        private readonly IStorageServiceClientProviderFactory clientProviderFactory;
        private readonly AzureStorageOptions azureStorageOptions;
        private readonly INameResolver nameResolver;
        private readonly ILoggerFactory loggerFactory;
        private readonly bool useSeparateQueueForEntityWorkItems;
        private readonly bool inConsumption; // If true, optimize defaults for consumption
        private AzureStorageDurabilityProvider defaultStorageProvider;

        // Must wait to get settings until we have validated taskhub name.
        private bool hasValidatedOptions;
        private AzureStorageOrchestrationServiceSettings defaultSettings;

        public AzureStorageDurabilityProviderFactory(
            IOptions<DurableTaskOptions> options,
            IStorageServiceClientProviderFactory clientProviderFactory,
            INameResolver nameResolver,
            ILoggerFactory loggerFactory,
#pragma warning disable CS0612 // Type or member is obsolete
            IPlatformInformation platformInfo)
#pragma warning restore CS0612 // Type or member is obsolete
        {
            // this constructor may be called by dependency injection even if the AzureStorage provider is not selected
            // in that case, return immediately, since this provider is not actually used, but can still throw validation errors
            if (options.Value.StorageProvider.TryGetValue("type", out object value)
                && value is string s
                && !string.Equals(s, this.Name, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            this.options = options.Value;
            this.clientProviderFactory = clientProviderFactory ?? throw new ArgumentNullException(nameof(clientProviderFactory));
            this.nameResolver = nameResolver ?? throw new ArgumentNullException(nameof(nameResolver));
            this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

            this.azureStorageOptions = new AzureStorageOptions();
            this.inConsumption = platformInfo.IsInConsumptionPlan();

            // The consumption plan has different performance characteristics so we provide
            // different defaults for key configuration values.
            int maxConcurrentOrchestratorsDefault = this.inConsumption ? 5 : 10 * Environment.ProcessorCount;
            int maxConcurrentActivitiesDefault = this.inConsumption ? 10 : 10 * Environment.ProcessorCount;
            int maxConcurrentEntitiesDefault = this.inConsumption ? 10 : 10 * Environment.ProcessorCount;
            int maxEntityOperationBatchSizeDefault = this.inConsumption ? 50 : 5000;

            if (this.inConsumption)
            {
                WorkerRuntimeType language = platformInfo.GetWorkerRuntimeType();
                if (language == WorkerRuntimeType.Python)
                {
                    this.azureStorageOptions.ControlQueueBufferThreshold = 32;
                }
                else
                {
                    this.azureStorageOptions.ControlQueueBufferThreshold = 128;
                }
            }

            WorkerRuntimeType runtimeType = platformInfo.GetWorkerRuntimeType();
            if (runtimeType == WorkerRuntimeType.DotNetIsolated ||
                runtimeType == WorkerRuntimeType.Java ||
                runtimeType == WorkerRuntimeType.Custom)
            {
                this.useSeparateQueueForEntityWorkItems = true;
            }

            // The following defaults are only applied if the customer did not explicitely set them on `host.json`
            this.options.MaxConcurrentOrchestratorFunctions = this.options.MaxConcurrentOrchestratorFunctions ?? maxConcurrentOrchestratorsDefault;
            this.options.MaxConcurrentActivityFunctions = this.options.MaxConcurrentActivityFunctions ?? maxConcurrentActivitiesDefault;
            this.options.MaxConcurrentEntityFunctions = this.options.MaxConcurrentEntityFunctions ?? maxConcurrentEntitiesDefault;
            this.options.MaxEntityOperationBatchSize = this.options.MaxEntityOperationBatchSize ?? maxEntityOperationBatchSizeDefault;

            // Override the configuration defaults with user-provided values in host.json, if any.
            JsonConvert.PopulateObject(JsonConvert.SerializeObject(this.options.StorageProvider), this.azureStorageOptions);

            var logger = loggerFactory.CreateLogger(nameof(this.azureStorageOptions));
            this.azureStorageOptions.Validate(logger);

            this.DefaultConnectionName = this.azureStorageOptions.ConnectionName ?? ConnectionStringNames.Storage;
        }

        public virtual string Name => ProviderName;

        internal string DefaultConnectionName { get; }

        // This method should not be called before the app settings are resolved into the options.
        // Because of this, we wait to validate the options until right before building a durability provider, rather
        // than in the Factory constructor.
        private void EnsureDefaultClientSettingsInitialized()
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

        public virtual DurabilityProvider GetDurabilityProvider()
        {
            this.EnsureDefaultClientSettingsInitialized();
            if (this.defaultStorageProvider == null)
            {
                var defaultService = new AzureStorageOrchestrationService(this.defaultSettings);
                ILogger logger = this.loggerFactory.CreateLogger(LoggerName);
                this.defaultStorageProvider = new AzureStorageDurabilityProvider(
                    defaultService,
                    this.clientProviderFactory,
                    this.DefaultConnectionName,
                    this.azureStorageOptions,
                    logger);
            }

            return this.defaultStorageProvider;
        }

        public virtual DurabilityProvider GetDurabilityProvider(DurableClientAttribute attribute)
        {
            if (!attribute.ExternalClient)
            {
                this.EnsureDefaultClientSettingsInitialized();
            }

            return this.GetAzureStorageStorageProvider(attribute);
        }

        private AzureStorageDurabilityProvider GetAzureStorageStorageProvider(DurableClientAttribute attribute)
        {
            string connectionName = attribute.ConnectionName ?? this.DefaultConnectionName;
            AzureStorageOrchestrationServiceSettings settings = this.GetAzureStorageOrchestrationServiceSettings(connectionName, attribute.TaskHub);

            AzureStorageDurabilityProvider innerClient;

            // Need to check this.defaultStorageProvider != null for external clients that call GetDurabilityProvider(attribute)
            // which never initializes the defaultStorageProvider.
            if (string.Equals(this.DefaultConnectionName, connectionName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(this.options.HubName, settings.TaskHubName, StringComparison.OrdinalIgnoreCase) &&
                this.defaultStorageProvider != null)
            {
                // It's important that clients use the same AzureStorageOrchestrationService instance
                // as the host when possible to ensure we any send operations can be picked up
                // immediately instead of waiting for the next queue polling interval.
                innerClient = this.defaultStorageProvider;
            }
            else
            {
                ILogger logger = this.loggerFactory.CreateLogger(LoggerName);
                innerClient = new AzureStorageDurabilityProvider(
                    new AzureStorageOrchestrationService(settings),
                    this.clientProviderFactory,
                    connectionName,
                    this.azureStorageOptions,
                    logger);
            }

            return innerClient;
        }

        internal AzureStorageOrchestrationServiceSettings GetAzureStorageOrchestrationServiceSettings(
            string connectionName = null,
            string taskHubNameOverride = null)
        {
            TimeSpan extendedSessionTimeout = TimeSpan.FromSeconds(
                Math.Max(this.options.ExtendedSessionIdleTimeoutInSeconds, 0));

            var settings = new AzureStorageOrchestrationServiceSettings
            {
                StorageAccountClientProvider = this.clientProviderFactory.GetClientProvider(connectionName ?? this.DefaultConnectionName),
                TaskHubName = taskHubNameOverride ?? this.options.HubName,
                PartitionCount = this.azureStorageOptions.PartitionCount,
                ControlQueueBatchSize = this.azureStorageOptions.ControlQueueBatchSize,
                ControlQueueBufferThreshold = this.azureStorageOptions.ControlQueueBufferThreshold,
                ControlQueueVisibilityTimeout = this.azureStorageOptions.ControlQueueVisibilityTimeout,
                WorkItemQueueVisibilityTimeout = this.azureStorageOptions.WorkItemQueueVisibilityTimeout,
                MaxConcurrentTaskOrchestrationWorkItems = this.options.MaxConcurrentOrchestratorFunctions ?? throw new InvalidOperationException($"{nameof(this.options.MaxConcurrentOrchestratorFunctions)} needs a default value"),
                MaxConcurrentTaskActivityWorkItems = this.options.MaxConcurrentActivityFunctions ?? throw new InvalidOperationException($"{nameof(this.options.MaxConcurrentOrchestratorFunctions)} needs a default value"),
                MaxConcurrentTaskEntityWorkItems = this.options.MaxConcurrentEntityFunctions ?? throw new InvalidOperationException($"{nameof(this.options.MaxConcurrentEntityFunctions)} needs a default value"),
                ExtendedSessionsEnabled = this.options.ExtendedSessionsEnabled,
                ExtendedSessionIdleTimeout = extendedSessionTimeout,
                MaxQueuePollingInterval = this.azureStorageOptions.MaxQueuePollingInterval,
                TrackingServiceClientProvider = this.azureStorageOptions.TrackingStoreConnectionName != null
                    ? this.clientProviderFactory.GetTrackingClientProvider(this.azureStorageOptions.TrackingStoreConnectionName)
                    : null,
                FetchLargeMessageDataEnabled = this.azureStorageOptions.FetchLargeMessagesAutomatically,
                ThrowExceptionOnInvalidDedupeStatus = true,
                UseAppLease = this.options.UseAppLease,
                AppLeaseOptions = this.options.AppLeaseOptions,
                AppName = EndToEndTraceHelper.LocalAppName,
                LoggerFactory = this.loggerFactory,
                UseLegacyPartitionManagement = this.azureStorageOptions.UseLegacyPartitionManagement,
                UseTablePartitionManagement = this.azureStorageOptions.UseTablePartitionManagement,
                UseSeparateQueueForEntityWorkItems = this.useSeparateQueueForEntityWorkItems,
                EntityMessageReorderWindowInMinutes = this.options.EntityMessageReorderWindowInMinutes,
                MaxEntityOperationBatchSize = this.options.MaxEntityOperationBatchSize,
                AllowReplayingTerminalInstances = this.azureStorageOptions.AllowReplayingTerminalInstances,
            };

            if (this.inConsumption)
            {
                settings.MaxStorageOperationConcurrency = 25;
            }

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
    }
 }
