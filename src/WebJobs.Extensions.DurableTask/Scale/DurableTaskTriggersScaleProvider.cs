// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable

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
            FunctionName functionName = new FunctionName(functionId);

            this.GetOptions(triggerMetadata);

            IDurabilityProviderFactory durabilityProviderFactory = this.GetDurabilityProviderFactory();
            DurabilityProvider defaultDurabilityProvider = durabilityProviderFactory.GetDurabilityProvider();

            string? connectionName = durabilityProviderFactory is AzureStorageDurabilityProviderFactory azureStorageDurabilityProviderFactory
                ? azureStorageDurabilityProviderFactory.DefaultConnectionName
                : null;

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
            var durabilityProviderFactory = DurableTaskExtension.GetDurabilityProviderFactory(this.options, logger, this.durabilityProviderFactories);
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