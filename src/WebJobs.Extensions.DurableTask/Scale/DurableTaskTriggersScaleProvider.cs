// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
#if FUNCTIONS_V3_OR_GREATER

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale
{
    internal class DurableTaskTriggersScaleProvider : IScaleMonitorProvider, ITargetScalerProvider
    {
        private readonly IScaleMonitor monitor;
        private readonly ITargetScaler targetScaler;

        public DurableTaskTriggersScaleProvider(
            IServiceProvider serviceProvider,
            TriggerMetadata triggerMetadata)
        {
            string functionId = triggerMetadata.FunctionName;
            FunctionName functionName = new FunctionName(functionId);

            DurableTaskOptions options = this.GetOptions(serviceProvider, triggerMetadata);

            IDurabilityProviderFactory durabilityProviderFactory = this.GetDurabilityProviderFactory(serviceProvider, options);
            DurabilityProvider defaultDurabilityProvider = durabilityProviderFactory.GetDurabilityProvider();

            string? connectionName = durabilityProviderFactory is AzureStorageDurabilityProviderFactory azureStorageDurabilityProviderFactory
                ? azureStorageDurabilityProviderFactory.DefaultConnectionName
                : null;

            this.targetScaler = ScaleUtils.GetTargetScaler(
                defaultDurabilityProvider,
                functionId,
                functionName,
                connectionName,
                options.HubName);

            this.monitor = ScaleUtils.GetScaleMonitor(
                defaultDurabilityProvider,
                functionId,
                functionName,
                connectionName,
                options.HubName);
        }

        private DurableTaskOptions GetOptions(IServiceProvider serviceProvider, TriggerMetadata triggerMetadata)
        {
            var nameResolver = serviceProvider.GetService<INameResolver>();

            // the metadata is the sync triggers payload
            var metadata = triggerMetadata.Metadata.ToObject<DurableTaskMetadata>();

            var options = serviceProvider.GetService<IOptions<DurableTaskOptions>>().Value;

            // The property `taskHubName` is always expected in the SyncTriggers payload
            options.HubName = metadata?.TaskHubName ?? throw new Exception($"Expected `taskHubName` property in SyncTriggers payload but found none. Payload: {triggerMetadata.Metadata}");
            if (metadata?.MaxConcurrentActivityFunctions != null)
            {
                options.MaxConcurrentActivityFunctions = metadata?.MaxConcurrentActivityFunctions;
            }

            if (metadata?.MaxConcurrentOrchestratorFunctions != null)
            {
                options.MaxConcurrentOrchestratorFunctions = metadata?.MaxConcurrentOrchestratorFunctions;
            }

            if (metadata?.StorageProvider != null)
            {
                options.StorageProvider = metadata?.StorageProvider;
            }

            DurableTaskOptions.ResolveAppSettingOptions(options, nameResolver);
            return options;
        }

        private IDurabilityProviderFactory GetDurabilityProviderFactory(IServiceProvider serviceProvider, DurableTaskOptions options)
        {
            var orchestrationServiceFactories = serviceProvider.GetService<IEnumerable<IDurabilityProviderFactory>>();
            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<DurableTaskTriggersScaleProvider>();
            var durabilityProviderFactory = DurableTaskExtension.GetDurabilityProviderFactory(options, logger, orchestrationServiceFactories);
            return durabilityProviderFactory;
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
            [JsonProperty]
            public string? TaskHubName { get; set; }

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue(null)]
            public int? MaxConcurrentOrchestratorFunctions { get; set; }

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue(null)]
            public int? MaxConcurrentActivityFunctions { get; set; }

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue(null)]
            public IDictionary<string, object>? StorageProvider { get; set; }
        }
    }
}
#endif