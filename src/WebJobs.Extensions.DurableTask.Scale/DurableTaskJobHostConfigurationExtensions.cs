// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Linq;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.AzureManaged;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.AzureStorage;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Sql;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

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

            // Note: IConfiguration should be provided by the Scale Controller via ConfigureAppConfiguration
            // which registers it when the host is built. Our factory functions are called lazily when services
            // are resolved (after host is built), so IConfiguration will be available at that time.
            // We don't register IConfiguration here - we rely on the Scale Controller to provide it.

            // Register StorageServiceClientProviderFactory using factory function to ensure proper construction
            serviceCollection.TryAddSingleton<IStorageServiceClientProviderFactory>(serviceProvider =>
            {
                return new StorageServiceClientProviderFactory(
                    serviceProvider.GetRequiredService<IConfiguration>(),
                    serviceProvider.GetRequiredService<ILoggerFactory>());
            });

            // Register all scalability provider factories using factory functions to ensure proper construction
            // This ensures factories are constructed even if some dependencies are resolved lazily
            serviceCollection.AddSingleton<IScalabilityProviderFactory>(serviceProvider =>
            {
                return new AzureStorageScalabilityProviderFactory(
                    serviceProvider.GetRequiredService<IStorageServiceClientProviderFactory>(),
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

            return builder;
        }

        /// <summary>
        /// Adds scale-monitoring components (<see cref="IScaleMonitor"/> and <see cref="ITargetScaler"/> providers) for Durable triggers.
        /// This enables the scale controller to montor load of durable backends and make scaling decisions.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <param name="triggerMetadata">Metadata describing the trigger to be monitored for scaling.</param>
        /// <returns>The same <see cref="IWebJobsBuilder"/> instance, to allow for fluent chaining.</returns>
        public static IWebJobsBuilder AddDurableScaleForTrigger(this IWebJobsBuilder builder, TriggerMetadata triggerMetadata)
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
                    var logger = loggerFactory?.CreateLogger(typeof(DurableTaskJobHostConfigurationExtensions));
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
