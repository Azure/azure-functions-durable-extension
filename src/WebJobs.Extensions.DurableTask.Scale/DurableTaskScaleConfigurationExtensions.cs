// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Linq;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.AzureManaged;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.AzureStorage;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Netherite;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Sql;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale
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
        public static IWebJobsBuilder AddDurableTask(this IWebJobsBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            // Register the extension with the WebJobs framework.
            // This is required for proper host lifecycle management.
            builder.AddExtension<DurableTaskScaleExtension>();

            IServiceCollection serviceCollection = builder.Services;

            // Register StorageServiceClientProviderFactory using factory function to ensure proper construction
            serviceCollection.TryAddSingleton<IStorageServiceClientProviderFactory>(serviceProvider =>
            {
                return new StorageServiceClientProviderFactory(
                    serviceProvider.GetRequiredService<IConfiguration>(),
                    serviceProvider.GetRequiredService<ILoggerFactory>());
            });

            // Register all scalability provider factories
            serviceCollection.AddSingleton<IScalabilityProviderFactory>(serviceProvider =>
            {
                return new AzureStorageScalabilityProviderFactory(
                    serviceProvider.GetRequiredService<IStorageServiceClientProviderFactory>(),
                    serviceProvider.GetRequiredService<IConfiguration>(),
                    serviceProvider.GetRequiredService<INameResolver>(),
                    serviceProvider.GetRequiredService<ILoggerFactory>());
            });

            serviceCollection.AddSingleton<IScalabilityProviderFactory>(serviceProvider =>
            {
                return new AzureManagedScalabilityProviderFactory(
                    serviceProvider.GetRequiredService<IConfiguration>(),
                    serviceProvider.GetRequiredService<INameResolver>(),
                    serviceProvider.GetRequiredService<ILoggerFactory>());
            });

            serviceCollection.AddSingleton<IScalabilityProviderFactory>(serviceProvider =>
            {
                return new SqlServerScalabilityProviderFactory(
                    serviceProvider.GetRequiredService<IConfiguration>(),
                    serviceProvider.GetRequiredService<INameResolver>(),
                    serviceProvider.GetRequiredService<ILoggerFactory>());
            });

            serviceCollection.AddSingleton<IScalabilityProviderFactory>(serviceProvider =>
            {
                // Pass IServiceProvider for runtime scaling identity support
                // Netherite uses it to resolve AzureComponentFactory for managed identity
                return new NetheriteScalabilityProviderFactory(
                    serviceProvider.GetRequiredService<IConfiguration>(),
                    serviceProvider.GetRequiredService<ILoggerFactory>(),
                    serviceProvider);
            });

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

            // this segment adheres to the followings pattern: https://github.com/Azure/azure-sdk-for-net/pull/38756
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
                        serviceProvider.GetService<INameResolver>(),
                        serviceProvider.GetService<ILoggerFactory>(),
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
                        "Failed to create DurableTaskTriggersScaleProvider for function {FunctionName}. " +
                        "This may prevent scaling from working correctly.",
                        triggerMetadata.FunctionName);
                    throw;
                }
            });

            // builder.Services.AddSingleton<IScaleMonitorProvider>(serviceProvider => serviceProvider.GetServices<DurableTaskTriggersScaleProvider>().Single(x => x == provider));
            builder.Services.AddSingleton<ITargetScalerProvider>(serviceProvider =>
            {
                // Get the DurableTaskTriggersScaleProvider instance - it should have been created by now
                var providers = serviceProvider.GetServices<DurableTaskTriggersScaleProvider>();
                if (providers == null || !providers.Any())
                {
                    throw new InvalidOperationException(
                        $"DurableTaskTriggersScaleProvider was not registered for function {triggerMetadata.FunctionName}. " +
                        "This may indicate that AddDurableScaleForTrigger() failed during registration.");
                }

                // Use SingleOrDefault to get the provider, or throw if there are multiple
                var targetProvider = providers.SingleOrDefault(x => x == provider) ?? providers.Single();
                return targetProvider;
            });
            return builder;
        }
    }
}
