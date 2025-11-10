// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.AzureManaged;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.AzureStorage;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Sql;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale
{
    /// <summary>
    /// Provides extension methods for registering the <see cref="DurableTaskScaleExtension"/> with an <see cref="IWebJobsBuilder"/> or <c>JobHostConfiguration</c>.
    /// </summary>
    public static class DurableTaskJobHostConfigurationExtensions
    {
        /// <summary>
        /// Adds the <see cref="DurableTaskScaleExtension"/> to the specified <see cref="IWebJobsBuilder"/>.
        /// This enables Durable Task–based scaling capabilities for WebJobs and Azure Functions hosts.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <returns>The same <see cref="IWebJobsBuilder"/> instance, to allow for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the provided <paramref name="builder"/> is <see langword="null"/>.
        /// </exception>
        public static IWebJobsBuilder AddDurableTask(this IWebJobsBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddExtension<DurableTaskScaleExtension>();

            IServiceCollection serviceCollection = builder.Services;
            serviceCollection.TryAddSingleton<IStorageServiceClientProviderFactory, StorageServiceClientProviderFactory>();

            // Register all scalability provider factories
            serviceCollection.AddSingleton<IScalabilityProviderFactory, AzureStorageScalabilityProviderFactory>();
            serviceCollection.AddSingleton<IScalabilityProviderFactory, AzureManagedScalabilityProviderFactory>();
            serviceCollection.AddSingleton<IScalabilityProviderFactory, SqlServerScalabilityProviderFactory>();

            return builder;
        }

        /// <summary>
        /// Adds scale-monitoring components (<see cref="IScaleMonitor"/> and <see cref="ITargetScaler"/> providers) for Durable triggers.
        /// This enables the scale controller to montor load of durable backends and make scaling decisions.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <param name="triggerMetadata">Metadata describing the trigger to be monitored for scaling.</param>
        /// <returns>The same <see cref="IWebJobsBuilder"/> instance, to allow for fluent chaining.</returns>
        internal static IWebJobsBuilder AddDurableScaleForTrigger(this IWebJobsBuilder builder, TriggerMetadata triggerMetadata)
        {
            // this segment adheres to the followings pattern: https://github.com/Azure/azure-sdk-for-net/pull/38756
            DurableTaskTriggersScaleProvider provider = null;
            builder.Services.AddSingleton(serviceProvider =>
            {
                provider = new DurableTaskTriggersScaleProvider(serviceProvider.GetService<INameResolver>(), serviceProvider.GetService<ILoggerFactory>(), serviceProvider.GetService<IEnumerable<IScalabilityProviderFactory>>(), triggerMetadata);
                return provider;
            });

            // builder.Services.AddSingleton<IScaleMonitorProvider>(serviceProvider => serviceProvider.GetServices<DurableTaskTriggersScaleProvider>().Single(x => x == provider));
            builder.Services.AddSingleton<ITargetScalerProvider>(serviceProvider => serviceProvider.GetServices<DurableTaskTriggersScaleProvider>().Single(x => x == provider));
            return builder;
        }
    }
}
