// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Linq;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.AzureManaged;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.AzureStorage;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Netherite;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Sql;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale
{
    /// <summary>
    /// Provides extension methods for registering Durable Task scaling components with an <see cref="IWebJobsBuilder"/>.
    /// </summary>
    public static class DurableTaskScaleConfigurationExtensions
    {
        /// <summary>
        /// Registers scalability provider factories for Durable Task scaling.
        /// This is called by the Scale Controller to enable scaling decisions based on Durable Task backend load.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <returns>The same <see cref="IWebJobsBuilder"/> instance, to allow for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the provided <paramref name="builder"/> is <see langword="null"/>.
        /// </exception>
        public static Microsoft.Azure.WebJobs.IWebJobsBuilder AddDurableTask(this IWebJobsBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            // Register the extension with the WebJobs framework.
            // This is required for proper host lifecycle management.
            builder.AddExtension<DurableTaskScaleExtension>();

            IServiceCollection serviceCollection = builder.Services;

            serviceCollection.TryAddSingleton<IStorageServiceClientProviderFactory, StorageServiceClientProviderFactory>();

            serviceCollection.AddSingleton<IScalabilityProviderFactory, AzureStorageScalabilityProviderFactory>();
            serviceCollection.AddSingleton<IScalabilityProviderFactory, AzureManagedScalabilityProviderFactory>();
            serviceCollection.AddSingleton<IScalabilityProviderFactory, NetheriteScalabilityProviderFactory>();

            serviceCollection.AddSingleton<IScalabilityProviderFactory, SqlServerScalabilityProviderFactory>();
            return builder;
        }

        /// <summary>
        /// Adds scale-monitoring components (<see cref="IScaleMonitor"/> and <see cref="ITargetScaler"/> providers) for Durable triggers.
        /// This enables the scale controller to monitor load of durable backends and make scaling decisions.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <param name="triggerMetadata">Metadata describing the trigger to be monitored for scaling.</param>
        /// <returns>The same <see cref="IWebJobsBuilder"/> instance, to allow for fluent chaining.</returns>
        internal static IWebJobsBuilder AddDurableScaleForTrigger(this IWebJobsBuilder builder, TriggerMetadata triggerMetadata)
        {
            IServiceCollection serviceCollection = builder.Services;

            // Ensure required dependencies are registered before resolving factories
            // Note: Factories are already registered by AddDurableTask() which is called first by Scale Controller
            // IConfiguration should be provided by the Scale Controller with app settings
            // StorageServiceClientProviderFactory is already registered by AddDurableTask(), but ensure it's available
            serviceCollection.TryAddSingleton<IStorageServiceClientProviderFactory>(serviceProvider =>
            {
                return new StorageServiceClientProviderFactory(
                    serviceProvider.GetRequiredService<IConfiguration>(),
                    serviceProvider.GetRequiredService<ILoggerFactory>());
            });

            // this segment adheres to the following pattern: https://github.com/Azure/azure-sdk-for-net/pull/38756
            DurableTaskTriggersScaleProvider provider = null;
            builder.Services.AddSingleton(serviceProvider =>
            {
                // Use GetServices (plural) which returns an empty enumerable if no services are registered,
                // instead of GetService which returns null
                var scalabilityProviderFactories = serviceProvider.GetServices<IScalabilityProviderFactory>();

                // Validate that factories were successfully resolved
                if (scalabilityProviderFactories == null || !scalabilityProviderFactories.Any())
                {
                    throw new InvalidOperationException(
                        "No scalability provider factories could be resolved. " +
                        "Ensure that AddDurableTask() was called or that all required dependencies (IConfiguration, INameResolver, ILoggerFactory) are registered.");
                }

                try
                {
                    provider = new DurableTaskTriggersScaleProvider(
                        serviceProvider.GetRequiredService<Microsoft.Azure.WebJobs.INameResolver>(),
                        serviceProvider.GetRequiredService<ILoggerFactory>(),
                        scalabilityProviderFactories,
                        triggerMetadata);
                    return provider;
                }
                catch (Exception ex)
                {
                    var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
                    var logger = loggerFactory?.CreateLogger(typeof(DurableTaskScaleConfigurationExtensions));
                    logger?.LogError(
                        ex,
                        "Failed to create DurableTaskTriggersScaleProvider for function {FunctionName}. This may prevent scaling from working correctly.",
                        triggerMetadata.FunctionName);
                    throw;
                }
            });

            // Not registering IScaleMonitorProvider because the scale controller uses target-based scaling (TBS) by default.
            // This is consistent with the behavior in DurableTaskJobHostConfigurationExtensions.cs at WebJobs.Extensions.DurableTask.
            // The scale monitor code is intentionally kept in case a regression requires re-enabling it.
            builder.Services.AddSingleton<ITargetScalerProvider>(serviceProvider => serviceProvider.GetServices<DurableTaskTriggersScaleProvider>().Single(x => x == provider));
            return builder;
        }
    }
}
