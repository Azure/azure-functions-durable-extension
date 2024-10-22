// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
#if !FUNCTIONS_V1
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Auth;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.ContextImplementations;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
#else
using Microsoft.Azure.WebJobs.Extensions.DurableTask.ContextImplementations;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
#endif

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Extension for registering a Durable Functions configuration with <c>JobHostConfiguration</c>.
    /// </summary>
    public static class DurableTaskJobHostConfigurationExtensions
    {
        /// <summary>
        /// Adds the Durable Task extension to the provided <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to configure.</param>
        /// <returns>Returns the provided <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddDurableClientFactory(this IServiceCollection serviceCollection)
        {
            if (serviceCollection == null)
            {
                throw new ArgumentNullException(nameof(serviceCollection));
            }

            serviceCollection.TryAddSingleton<INameResolver, DefaultNameResolver>();
#if !FUNCTIONS_V1
            serviceCollection.AddAzureClientsCore();
            serviceCollection.TryAddSingleton<ITokenCredentialFactory, AzureCredentialFactory>();
#endif
            serviceCollection.TryAddSingleton<IDurabilityProviderFactory, AzureStorageDurabilityProviderFactory>();
            serviceCollection.TryAddSingleton<IDurableClientFactory, DurableClientFactory>();
            serviceCollection.TryAddSingleton<IMessageSerializerSettingsFactory, MessageSerializerSettingsFactory>();
#pragma warning disable CS0612, CS0618 // Type or member is obsolete
            serviceCollection.TryAddSingleton<IConnectionStringResolver, StandardConnectionStringProvider>();
            serviceCollection.TryAddSingleton<IPlatformInformation, DefaultPlatformInformation>();
#pragma warning restore CS0612, CS0618 // Type or member is obsolete

            return serviceCollection;
        }

        /// <summary>
        /// Adds the Durable Task extension to the provided <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to configure.</param>
        /// <param name="optionsBuilder">Populate default configurations of <see cref="DurableClientOptions"/> to create Durable Clients.</param>
        /// <returns>Returns the provided <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddDurableClientFactory(this IServiceCollection serviceCollection, Action<DurableClientOptions> optionsBuilder)
        {
            AddDurableClientFactory(serviceCollection);
            serviceCollection.Configure<DurableClientOptions>(optionsBuilder.Invoke);
            return serviceCollection;
        }

#if !FUNCTIONS_V1
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
                .AddExtension<DurableTaskExtension>()
                .BindOptions<DurableTaskOptions>();

            IServiceCollection serviceCollection = builder.Services;
            serviceCollection.AddAzureClientsCore();
            serviceCollection.TryAddSingleton<IConnectionInfoResolver, WebJobsConnectionInfoProvider>();
            serviceCollection.TryAddSingleton<ITokenCredentialFactory, AzureCredentialFactory>();
            serviceCollection.TryAddSingleton<IStorageAccountProvider, AzureStorageAccountProvider>();
            serviceCollection.TryAddSingleton<IDurableHttpMessageHandlerFactory, DurableHttpMessageHandlerFactory>();
            serviceCollection.AddSingleton<IDurabilityProviderFactory, AzureStorageDurabilityProviderFactory>();
            serviceCollection.TryAddSingleton<IMessageSerializerSettingsFactory, MessageSerializerSettingsFactory>();
            serviceCollection.TryAddSingleton<IErrorSerializerSettingsFactory, ErrorSerializerSettingsFactory>();
            serviceCollection.TryAddSingleton<IApplicationLifetimeWrapper, HostLifecycleService>();
            serviceCollection.AddSingleton<ITelemetryActivator, TelemetryActivator>();
            serviceCollection.TryAddSingleton<IDurableClientFactory, DurableClientFactory>();
#pragma warning disable CS0612, CS0618 // Type or member is obsolete
            serviceCollection.TryAddSingleton<IConnectionStringResolver, WebJobsConnectionStringProvider>();
            serviceCollection.AddSingleton<IPlatformInformation, DefaultPlatformInformation>();
#pragma warning restore CS0612, CS0618 // Type or member is obsolete

            return builder;
        }

#if FUNCTIONS_V3_OR_GREATER
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
            builder.Services.AddSingleton<IScaleMonitorProvider>(serviceProvider => serviceProvider.GetServices<DurableTaskTriggersScaleProvider>().Single(x => x == provider));
            builder.Services.AddSingleton<ITargetScalerProvider>(serviceProvider => serviceProvider.GetServices<DurableTaskTriggersScaleProvider>().Single(x => x == provider));
            return builder;
        }
#endif

        /// <summary>
        /// Adds the Durable Task extension to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <param name="options">The configuration options for this extension.</param>
        /// <returns>Returns the provided <see cref="IWebJobsBuilder"/>.</returns>
        public static IWebJobsBuilder AddDurableTask(this IWebJobsBuilder builder, IOptions<DurableTaskOptions> options)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            builder.AddDurableTask();
            builder.Services.AddSingleton(options);
            return builder;
        }

        /// <summary>
        /// Adds the Durable Task extension to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <param name="configure">An <see cref="Action{DurableTaskOptions}"/> to configure the provided <see cref="DurableTaskOptions"/>.</param>
        /// <returns>Returns the modified <paramref name="builder"/> object.</returns>
        public static IWebJobsBuilder AddDurableTask(this IWebJobsBuilder builder, Action<DurableTaskOptions> configure)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            builder.AddDurableTask();
            builder.Services.Configure(configure);

            return builder;
        }

#else
        /// <summary>
        /// Enable running durable orchestrations implemented as functions.
        /// </summary>
        /// <param name="hostConfig">Configuration settings of the current <c>JobHost</c> instance.</param>
        /// <param name="listenerConfig">Durable Functions configuration.</param>
        public static void UseDurableTask(
            this JobHostConfiguration hostConfig,
            DurableTaskExtension listenerConfig)
        {
            if (hostConfig == null)
            {
                throw new ArgumentNullException(nameof(hostConfig));
            }

            if (listenerConfig == null)
            {
                throw new ArgumentNullException(nameof(listenerConfig));
            }

            IExtensionRegistry extensions = hostConfig.GetService<IExtensionRegistry>();
            extensions.RegisterExtension<IExtensionConfigProvider>(listenerConfig);
        }
#endif
    }
}
