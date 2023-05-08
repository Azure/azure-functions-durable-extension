// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if FUNCTIONS_V3_OR_GREATER

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Listener
{
    internal class DurableTaskTriggersScaleProvider : IScaleMonitorProvider, ITargetScalerProvider
    {
        private IScaleMonitor monitor;
        private ITargetScaler targetScaler;
        private DurabilityProvider defaultDurabilityProvider;
        private DurableTaskOptions options;

        public DurableTaskTriggersScaleProvider(
            IServiceProvider serviceProvider,
            TriggerMetadata triggerMetadata)
        {
            string functionId = triggerMetadata.FunctionName;
            FunctionName functionName = new FunctionName(functionId);

            var nameResolver = serviceProvider.GetService<INameResolver>();
            var orchestrationServiceFactories = serviceProvider.GetService<IEnumerable<IDurabilityProviderFactory>>();

            this.options = serviceProvider.GetService<IOptions<DurableTaskOptions>>().Value;
            DurableTaskOptions.ResolveAppSettingOptions(this.options, nameResolver);

            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger(DurableTaskExtension.LoggerCategoryName);

            var durabilityProviderFactory = DurableTaskExtension.GetDurabilityProviderFactory(this.options, logger, orchestrationServiceFactories);
            this.defaultDurabilityProvider = durabilityProviderFactory.GetDurabilityProvider();

            var connectionName = durabilityProviderFactory is AzureStorageDurabilityProviderFactory azureStorageDurabilityProviderFactory
                ? azureStorageDurabilityProviderFactory.DefaultConnectionName
                : null;

            var scaleUtils = new ScaleUtils();

            this.monitor = scaleUtils.GetScaleMonitor(
                this.defaultDurabilityProvider,
                functionId,
                functionName,
                connectionName,
                this.options.HubName);

            this.targetScaler = scaleUtils.GetTargetScaler(
                this.defaultDurabilityProvider,
                functionId,
                functionName,
                connectionName,
                this.options.HubName);
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
#endif