// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale
{
    /// <summary>
    /// WebJobs extension for Durable Task scaling.
    /// This extension is registered by the Scale Controller to enable scaling decisions.
    /// </summary>
    public class DurableTaskScaleExtension : IExtensionConfigProvider
    {
        private const string DefaultProvider = "AzureStorage";
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DurableTaskScaleExtension"/> class.
        /// </summary>
        /// <param name="loggerFactory">The logger factory for creating loggers.</param>
        public DurableTaskScaleExtension(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger<DurableTaskScaleExtension>();
            this.logger.LogInformation("DurableTaskScaleExtension initialized.");
        }

        /// <summary>
        /// Determines the scalability provider factory based on the given metadata.
        /// If no storage provider type is configured, defaults to AzureStorage.
        /// </summary>
        /// <param name="metadata">The metadata specifying the target storage provider and hub configuration.</param>
        /// <param name="logger">The logger instance for diagnostic messages.</param>
        /// <param name="scalabilityProviderFactories">A collection of available scalability provider factories.</param>
        /// <returns>The resolved <see cref="IScalabilityProviderFactory"/> suitable for the configured provider.</returns>
        public static IScalabilityProviderFactory GetScalabilityProviderFactory(
            DurableTaskMetadata metadata,
            ILogger logger,
            IEnumerable<IScalabilityProviderFactory> scalabilityProviderFactories)
        {
            object? storageType = null;
            bool storageTypeIsConfigured = metadata.StorageProvider != null && metadata.StorageProvider.TryGetValue("type", out storageType);

            if (!storageTypeIsConfigured)
            {
                try
                {
                    IScalabilityProviderFactory defaultFactory = scalabilityProviderFactories.First(f => f.Name.Equals(DefaultProvider));
                    logger.LogInformation($"Using the default storage provider: {DefaultProvider}.");
                    return defaultFactory;
                }
                catch (InvalidOperationException e)
                {
                    throw new InvalidOperationException($"Couldn't find the default storage provider: {DefaultProvider}.", e);
                }
            }

            try
            {
                IScalabilityProviderFactory selectedFactory = scalabilityProviderFactories.First(f => string.Equals(f.Name, storageType!.ToString(), StringComparison.OrdinalIgnoreCase));
                logger.LogInformation($"Using the {storageType} storage provider.");
                return selectedFactory;
            }
            catch (InvalidOperationException e)
            {
                IList<string> factoryNames = scalabilityProviderFactories.Select(f => f.Name).ToList();
                throw new InvalidOperationException($"Storage provider type ({storageType?.ToString() ?? "null"}) was not found. Available storage providers: {string.Join(", ", factoryNames)}.", e);
            }
        }

        /// <summary>
        /// Initializes the extension. Called by the WebJobs framework during host startup.
        /// </summary>
        /// <param name="context">The extension configuration context.</param>
        public void Initialize(ExtensionConfigContext context)
        {
            // The actual scaling work is done by DurableTaskTriggersScaleProvider.
            // This extension just needs to exist for the WebJobs framework to properly
            // initialize the host lifecycle.
            this.logger.LogInformation("DurableTaskScaleExtension.Initialize called.");
        }
    }
}
