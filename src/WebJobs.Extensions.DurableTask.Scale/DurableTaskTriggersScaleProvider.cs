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
        private readonly DurableTaskScaleOptions options;
        private readonly INameResolver nameResolver;
        private readonly ILoggerFactory loggerFactory;
        private readonly IEnumerable<IScalabilityProviderFactory> scalabilityProviderFactories;

        public DurableTaskTriggersScaleProvider(
            IOptions<DurableTaskScaleOptions> durableTaskScaleOptions,
            INameResolver nameResolver,
            ILoggerFactory loggerFactory,
            IEnumerable<IScalabilityProviderFactory> scalabilityProviderFactories,
            TriggerMetadata triggerMetadata)
        {
            this.options = durableTaskScaleOptions.Value;
            this.nameResolver = nameResolver;
            this.loggerFactory = loggerFactory;
            this.scalabilityProviderFactories = scalabilityProviderFactories;

            string functionId = triggerMetadata.FunctionName;
            var functionName = new FunctionName(functionId);

            this.GetOptions(triggerMetadata);

            IScalabilityProviderFactory scalabilityProviderFactory = this.GetScalabilityProviderFactory();

            // Always use the triggerMetadata overload for scale scenarios
            // The factory will extract TokenCredential if present
            ScalabilityProvider defaultscalabilityProvider = scalabilityProviderFactory.GetDurabilityProvider(triggerMetadata);

            // Note: `this.options` is populated from the trigger metadata above
            string? connectionName = GetConnectionName(scalabilityProviderFactory, this.options);

            // Check if using managed identity (for logging)
            bool usesManagedIdentity = triggerMetadata.Properties != null && 
                                       triggerMetadata.Properties.ContainsKey("AzureComponentFactory");

            var logger = loggerFactory.CreateLogger<DurableTaskTriggersScaleProvider>();
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
                this.options.HubName);

            this.monitor = ScaleUtils.GetScaleMonitor(
                defaultscalabilityProvider,
                functionId,
                functionName,
                connectionName,
                this.options.HubName);
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

            return null;
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

            DurableTaskScaleOptions.ResolveAppSettingOptions(this.options, this.nameResolver);
        }

        private IScalabilityProviderFactory GetScalabilityProviderFactory()
        {
            var logger = this.loggerFactory.CreateLogger<DurableTaskTriggersScaleProvider>();
            return DurableTaskScaleExtension.GetScalabilityProviderFactory(this.options, logger, this.scalabilityProviderFactories);
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