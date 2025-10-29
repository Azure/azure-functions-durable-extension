// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale
{
    /// <summary>
    /// Extension for registering a Durable Functions configuration with <c>JobHostConfiguration</c>.
    /// </summary>
    public static class DurableTaskJobHostConfigurationExtensions
    {

        /// <summary>
        /// Adds the Durable Task extension to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <returns>Returns the provided <see cref="IWebJobsBuilder"/>.</returns>
        public static IWebJobsBuilder AddDurableTask(this IWebJobsBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder
                .AddExtension<DurableTaskScaleExtension>()
                .BindOptions<DurableTaskOptions>();

            IServiceCollection serviceCollection = builder.Services;
            serviceCollection.AddAzureClientsCore();
            serviceCollection.TryAddSingleton<IConnectionInfoResolver, WebJobsConnectionInfoProvider>();
            serviceCollection.TryAddSingleton<IStorageServiceClientProviderFactory, StorageServiceClientProviderFactory>();
            serviceCollection.TryAddSingleton<IDurableHttpMessageHandlerFactory, DurableHttpMessageHandlerFactory>();
            serviceCollection.AddSingleton<IDurabilityProviderFactory, AzureStorageDurabilityProviderFactory>();
            serviceCollection.TryAddSingleton<IMessageSerializerSettingsFactory, MessageSerializerSettingsFactory>();
            serviceCollection.TryAddSingleton<IErrorSerializerSettingsFactory, ErrorSerializerSettingsFactory>();
            serviceCollection.TryAddSingleton<IApplicationLifetimeWrapper, HostLifecycleService>();
            return builder;
        }

        /// <summary>
        /// Adds the <see cref="IScaleMonitor"/> and <see cref="ITargetScaler"/> providers for the Durable Triggers.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <returns>Returns the provided <see cref="IWebJobsBuilder"/>.</returns>
        internal static IWebJobsBuilder AddDurableScaleForTrigger(this IWebJobsBuilder builder, TriggerMetadata triggerMetadata)
        {
            // this segment adheres to the followings pattern: https://github.com/Azure/azure-sdk-for-net/pull/38756
            DurableTaskTriggersScaleProvider provider = null;
            builder.Services.AddSingleton(serviceProvider =>
            {
                provider = new DurableTaskTriggersScaleProvider(serviceProvider.GetService<IOptions<DurableTaskOptions>>(), serviceProvider.GetService<INameResolver>(), serviceProvider.GetService<ILoggerFactory>(), serviceProvider.GetService<IEnumerable<IDurabilityProviderFactory>>(), triggerMetadata);
                return provider;
            });

            // Commenting out incremental scale model for hotfix release 3.0.0-rc.4, SC uses TBS by default
            // builder.Services.AddSingleton<IScaleMonitorProvider>(serviceProvider => serviceProvider.GetServices<DurableTaskTriggersScaleProvider>().Single(x => x == provider));
            builder.Services.AddSingleton<ITargetScalerProvider>(serviceProvider => serviceProvider.GetServices<DurableTaskTriggersScaleProvider>().Single(x => x == provider));
            return builder;
        }
    }
}
