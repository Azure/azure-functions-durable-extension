// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.AzureManaged;
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

        public DurableTaskTriggersScaleProvider(
            IOptions<DurableTaskScaleOptions> durableTaskScaleOptions,
            INameResolver nameResolver,
            ILoggerFactory loggerFactory,
            IEnumerable<IScalabilityProviderFactory> scalabilityProviderFactories,
            TriggerMetadata triggerMetadata)
        {
            string functionId = triggerMetadata.FunctionName;
            var functionName = new FunctionName(functionId);

            // Deserialize the configuration from triggerMetadata (sent by Scale Controller)
            // This is the source of truth for scale scenarios
            var metadata = triggerMetadata.Metadata.ToObject<DurableTaskMetadata>()
                ?? throw new InvalidOperationException($"Failed to deserialize trigger metadata. Payload: {triggerMetadata.Metadata}");

            // Validate required fields
            string hubName = metadata.TaskHubName
                ?? throw new InvalidOperationException($"Expected `taskHubName` property in SyncTriggers payload but found none. Payload: {metadata.TaskHubName}");

            // Store the parsed metadata in Properties so factories can use it (avoid re-parsing)
            // TriggerMetadata.Properties is read-only but the dictionary itself is mutable
            if (triggerMetadata.Properties != null)
            {
                triggerMetadata.Properties["DurableTaskMetadata"] = metadata;
            }

            // Build options from triggerMetadata for factory selection
            var options = new DurableTaskScaleOptions
            {
                HubName = hubName,
                MaxConcurrentActivityFunctions = metadata.MaxConcurrentActivityFunctions,
                MaxConcurrentOrchestratorFunctions = metadata.MaxConcurrentOrchestratorFunctions,
                StorageProvider = metadata.StorageProvider ?? new Dictionary<string, object>()
            };

            // Resolve app settings (e.g., %MyConnectionString% -> actual value)
            DurableTaskScaleOptions.ResolveAppSettingOptions(options, nameResolver);

            var logger = loggerFactory.CreateLogger<DurableTaskTriggersScaleProvider>();
            IScalabilityProviderFactory scalabilityProviderFactory = DurableTaskScaleExtension.GetScalabilityProviderFactory(
                options, logger, scalabilityProviderFactories);

            // Always use the triggerMetadata overload for scale scenarios
            // The factory will extract the parsed DurableTaskMetadata and TokenCredential if present
            ScalabilityProvider defaultscalabilityProvider = scalabilityProviderFactory.GetDurabilityProvider(triggerMetadata);

            // Get connection name from options (already extracted from metadata)
            string? connectionName = GetConnectionName(scalabilityProviderFactory, options);

            // Check if using managed identity (for logging)
            bool usesManagedIdentity = triggerMetadata.Properties != null &&
                                       triggerMetadata.Properties.ContainsKey("AzureComponentFactory");

            logger.LogInformation(
                "Creating DurableTaskTriggersScaleProvider for function {FunctionName}: connectionName = '{ConnectionName}', usesManagedIdentity = '{UsesMI}'",
                triggerMetadata.FunctionName,
                connectionName,
                usesManagedIdentity);

            this.targetScaler = ScaleUtils.GetTargetScaler(
                defaultscalabilityProvider,
                functionId,
                functionName,
                connectionName,
                hubName);

            this.monitor = ScaleUtils.GetScaleMonitor(
                defaultscalabilityProvider,
                functionId,
                functionName,
                connectionName,
                hubName);
        }

        private static string? GetConnectionName(IScalabilityProviderFactory scalabilityProviderFactory, DurableTaskScaleOptions options)
        {
            if (scalabilityProviderFactory is AzureStorageScalabilityProviderFactory azureStorageScalabilityProviderFactory)
            {
                if (options != null && options.StorageProvider != null)
                {
                    if (options.StorageProvider.TryGetValue("connectionName", out object value1) && value1 is string s1 && !string.IsNullOrWhiteSpace(s1))
                    {
                        return s1;
                    }

                    // legacy alias often used in payloads
                    if (options.StorageProvider.TryGetValue("connectionStringName", out object value2) && value2 is string s2 && !string.IsNullOrWhiteSpace(s2))
                    {
                        return s2;
                    }
                }

                return azureStorageScalabilityProviderFactory.DefaultConnectionName;
            }

            if (scalabilityProviderFactory is AzureManagedScalabilityProviderFactory azureManagedScalabilityProviderFactory)
            {
                if (options != null && options.StorageProvider != null)
                {
                    if (options.StorageProvider.TryGetValue("connectionName", out object value1) && value1 is string s1 && !string.IsNullOrWhiteSpace(s1))
                    {
                        return s1;
                    }

                    // legacy alias often used in payloads
                    if (options.StorageProvider.TryGetValue("connectionStringName", out object value2) && value2 is string s2 && !string.IsNullOrWhiteSpace(s2))
                    {
                        return s2;
                    }
                }

                return azureManagedScalabilityProviderFactory.DefaultConnectionName;
            }

            return null;
        }

        public IScaleMonitor GetMonitor()
        {
            return this.monitor;
        }

        public ITargetScaler GetTargetScaler()
        {
            return this.targetScaler;
        }
    }

    /// <summary>
    /// Represents the Durable Task configuration sent by the Scale Controller in the SyncTriggers payload.
    /// This is deserialized from triggerMetadata.Metadata and passed to factories via triggerMetadata.Properties.
    /// </summary>
    public class DurableTaskMetadata
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