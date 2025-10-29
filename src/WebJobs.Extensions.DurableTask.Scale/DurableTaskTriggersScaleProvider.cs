// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.AzureStorage;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale
{
    internal class DurableTaskTriggersScaleProvider : IScaleMonitorProvider, ITargetScalerProvider
    {
        private const string AzureManagedProviderName = "azureManaged";

        private readonly IScaleMonitor monitor;
        private readonly ITargetScaler targetScaler;
        private readonly DurableTaskOptions options;
        private readonly INameResolver nameResolver;
        private readonly ILoggerFactory loggerFactory;
        private readonly IEnumerable<IDurabilityProviderFactory> durabilityProviderFactories;

        public DurableTaskTriggersScaleProvider(
            IOptions<DurableTaskOptions> durableTaskOptions,
            INameResolver nameResolver,
            ILoggerFactory loggerFactory,
            IEnumerable<IDurabilityProviderFactory> durabilityProviderFactories,
            TriggerMetadata triggerMetadata)
        {
            this.options = durableTaskOptions.Value;
            this.nameResolver = nameResolver;
            this.loggerFactory = loggerFactory;
            this.durabilityProviderFactories = durabilityProviderFactories;

            string functionId = triggerMetadata.FunctionName;
            var functionName = new FunctionName(functionId);

            this.GetOptions(triggerMetadata);

            IDurabilityProviderFactory durabilityProviderFactory = this.GetDurabilityProviderFactory();

            DurabilityProvider defaultDurabilityProvider;
            if (string.Equals(durabilityProviderFactory.Name, AzureManagedProviderName, StringComparison.OrdinalIgnoreCase))
            {
                defaultDurabilityProvider = durabilityProviderFactory.GetDurabilityProvider(attribute: null, triggerMetadata);
            }
            else
            {
                defaultDurabilityProvider = durabilityProviderFactory.GetDurabilityProvider();
            }

            // Note: `this.options` is populated from the trigger metadata above
            string? connectionName = GetConnectionName(durabilityProviderFactory, this.options);

            var logger = loggerFactory.CreateLogger<DurableTaskTriggersScaleProvider>();
            logger.LogInformation(
                "Creating DurableTaskTriggersScaleProvider for function {FunctionName}: connectionName = '{ConnectionName}'",
                triggerMetadata.FunctionName,
                connectionName);

            this.targetScaler = ScaleUtils.GetTargetScaler(
                defaultDurabilityProvider,
                functionId,
                functionName,
                connectionName,
                this.options.HubName);

            this.monitor = ScaleUtils.GetScaleMonitor(
                defaultDurabilityProvider,
                functionId,
                functionName,
                connectionName,
                this.options.HubName);
        }

        private static string? GetConnectionName(IDurabilityProviderFactory durabilityProviderFactory, DurableTaskOptions options)
        {
            if (durabilityProviderFactory is AzureStorageDurabilityProviderFactory azureStorageDurabilityProviderFactory)
            {
                // First, look for the connection name in the options
                var azureStorageOptions = new AzureStorageOptions();
                if (options != null && options.StorageProvider != null)
                {
                    var json = JsonSerializer.Serialize(options.StorageProvider);
                    var newOptions = JsonSerializer.Deserialize<AzureStorageOptions>(json);
                    if (newOptions != null)
                    {
                        foreach (var prop in typeof(AzureStorageOptions).GetProperties())
                        {
                            var value = prop.GetValue(newOptions);
                            if (value != null)
                            {
                                prop.SetValue(azureStorageOptions, value);
                            }
                        }
                    }
                }

                // If the connection name is not found in the options, use the default connection name from the factory
                return azureStorageOptions.ConnectionName ?? azureStorageDurabilityProviderFactory.DefaultConnectionName;
            }
            else
            {
                return null;
            }
        }

        private void GetOptions(TriggerMetadata triggerMetadata)
        {
            // the metadata is the sync triggers payload
            var metadata = triggerMetadata.Metadata.ToObject<DurableTaskMetadata>();

            // The property `taskHubName` is always expected in the SyncTriggers payload
            this.options.HubName = metadata?.TaskHubName ?? throw new InvalidOperationException($"Expected `taskHubName` property in SyncTriggers payload but found none. Payload: {triggerMetadata.Metadata}");
            if (metadata?.MaxConcurrentActivityFunctions != null)
            {
                this.options.MaxConcurrentActivityFunctions = metadata?.MaxConcurrentActivityFunctions;
            }

            if (metadata?.MaxConcurrentOrchestratorFunctions != null)
            {
                this.options.MaxConcurrentOrchestratorFunctions = metadata?.MaxConcurrentOrchestratorFunctions;
            }

            if (metadata?.StorageProvider != null)
            {
                this.options.StorageProvider = metadata?.StorageProvider;
            }

            DurableTaskOptions.ResolveAppSettingOptions(this.options, this.nameResolver);
        }

        private IDurabilityProviderFactory GetDurabilityProviderFactory()
        {
            var logger = this.loggerFactory.CreateLogger<DurableTaskTriggersScaleProvider>();
            return GetDurabilityProviderFactory(this.options, logger, this.durabilityProviderFactories);
        }

        private static IDurabilityProviderFactory GetDurabilityProviderFactory(DurableTaskOptions options, ILogger logger, IEnumerable<IDurabilityProviderFactory> orchestrationServiceFactories)
        {
            const string DefaultProvider = "AzureStorage";

            bool storageTypeIsConfigured = options.StorageProvider.TryGetValue("type", out object storageType);

            if (!storageTypeIsConfigured)
            {
                try
                {
                    IDurabilityProviderFactory defaultFactory = orchestrationServiceFactories.First(f => f.Name.Equals(DefaultProvider));
                    logger.LogInformation($"Using the default storage provider: {DefaultProvider}.");
                    return defaultFactory;
                }
                catch (InvalidOperationException e)
                {
                    throw new InvalidOperationException($"Couldn't find the default storage provider: {DefaultProvider}.", e);
                }
            }

            try
            {
                IDurabilityProviderFactory selectedFactory = orchestrationServiceFactories.First(f => string.Equals(f.Name, storageType.ToString(), StringComparison.OrdinalIgnoreCase));
                logger.LogInformation($"Using the {storageType} storage provider.");
                return selectedFactory;
            }
            catch (InvalidOperationException e)
            {
                IList<string> factoryNames = orchestrationServiceFactories.Select(f => f.Name).ToList();
                throw new InvalidOperationException($"Storage provider type ({storageType}) was not found. Available storage providers: {string.Join(", ", factoryNames)}.", e);
            }
        }

        public IScaleMonitor GetMonitor()
        {
            return this.monitor;
        }

        public ITargetScaler GetTargetScaler()
        {
            return this.targetScaler;
        }

        /// <summary>
        /// Captures the relevant DF SyncTriggers JSON properties for making scaling decisions.
        /// </summary>
        internal class DurableTaskMetadata
        {
            [JsonPropertyName("taskHubName")]
            public string? TaskHubName { get; set; }

            [JsonPropertyName("maxConcurrentOrchestratorFunctions")]
            public int? MaxConcurrentOrchestratorFunctions { get; set; }

            [JsonPropertyName("maxConcurrentActivityFunctions")]
            public int? MaxConcurrentActivityFunctions { get; set; }

            [JsonPropertyName("storageProvider")]
            public IDictionary<string, object>? StorageProvider { get; set; }
        }
    }
}