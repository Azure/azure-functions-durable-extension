// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale
{
    internal class DurableTaskTriggersScaleProvider : IScaleMonitorProvider, ITargetScalerProvider
    {
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

            // Deserialize the configuration from triggerMetadata 
            var metadata = triggerMetadata.Metadata.ToObject<DurableTaskMetadata>()
                ?? throw new InvalidOperationException($"Failed to deserialize trigger metadata. Payload: {triggerMetadata.Metadata}");

            // Build options from triggerMetadata with optional fallback to DI options
            // NOTE: durableTaskScaleOptions.Value will be null/empty in Scale Controller context
            // because Scale Controller doesn't have access to host.json
            var options = new DurableTaskScaleOptions
            {
                HubName = metadata.TaskHubName ?? durableTaskScaleOptions.Value?.HubName
                    ?? throw new InvalidOperationException($"Expected `taskHubName` property in SyncTriggers payload but found none. "),
                MaxConcurrentActivityFunctions = metadata.MaxConcurrentActivityFunctions ?? durableTaskScaleOptions.Value?.MaxConcurrentActivityFunctions,
                MaxConcurrentOrchestratorFunctions = metadata.MaxConcurrentOrchestratorFunctions ?? durableTaskScaleOptions.Value?.MaxConcurrentOrchestratorFunctions,
                StorageProvider = metadata.StorageProvider ?? durableTaskScaleOptions.Value?.StorageProvider ?? new Dictionary<string, object>(),
            };

            // Resolve app settings (e.g., %MyConnectionString% -> actual value)
            DurableTaskScaleOptions.ResolveAppSettingOptions(options, nameResolver);

            // Store the parsed options in Properties so factories can use them (avoid re-parsing)
            // TriggerMetadata.Properties is read-only but the dictionary itself is mutable
            if (triggerMetadata.Properties != null)
            {
                triggerMetadata.Properties["DurableTaskScaleOptions"] = options;
            }

            var logger = loggerFactory.CreateLogger<DurableTaskTriggersScaleProvider>();
            IScalabilityProviderFactory scalabilityProviderFactory = DurableTaskScaleExtension.GetScalabilityProviderFactory(
                options, logger, scalabilityProviderFactories);

            // Always use the triggerMetadata overload for scale scenarios
            // The factory will extract the parsed DurableTaskMetadata and TokenCredential if present
            ScalabilityProvider defaultscalabilityProvider = scalabilityProviderFactory.GetScalabilityProvider(triggerMetadata);

            // Get connection name (options.StorageProvider already has fallback to DI options built in)
            string? connectionName = GetConnectionNameFromOptions(options.StorageProvider) ?? scalabilityProviderFactory.DefaultConnectionName;

            logger.LogInformation(
                "Creating DurableTaskTriggersScaleProvider for function {FunctionName}: connectionName = '{ConnectionName}'",
                triggerMetadata.FunctionName,
                connectionName);

            this.targetScaler = ScaleUtils.GetTargetScaler(
                defaultscalabilityProvider,
                functionId,
                functionName,
                connectionName,
                options.HubName);

            this.monitor = ScaleUtils.GetScaleMonitor(
                defaultscalabilityProvider,
                functionId,
                functionName,
                connectionName,
                options.HubName);
        }

        private static string? GetConnectionNameFromOptions(IDictionary<string, object>? storageProvider)
        {
            if (storageProvider == null)
            {
                return null;
            }

            // Try connectionName first
            if (storageProvider.TryGetValue("connectionName", out object value1) && value1 is string s1 && !string.IsNullOrWhiteSpace(s1))
            {
                return s1;
            }

            // Try connectionStringName
            if (storageProvider.TryGetValue("connectionStringName", out object value2) && value2 is string s2 && !string.IsNullOrWhiteSpace(s2))
            {
                return s2;
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
}