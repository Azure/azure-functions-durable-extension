// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale
{
    /// <summary>
    /// Provides configuration and initialization logic for the Durable Task Scale extension.
    /// This extension enables scale controller to make scaling decisions based on the current load of Durable Task backends.
    /// </summary>
    public class DurableTaskScaleExtension : IExtensionConfigProvider
    {
        private readonly IScalabilityProviderFactory scalabilityProviderFactory;
        private readonly ScalabilityProvider defaultscalabilityProvider;
        private readonly DurableTaskMetadata metadata;
        private readonly ILogger logger;
        private readonly IEnumerable<IScalabilityProviderFactory> scalabilityProviderFactories;

        /// <summary>
        /// Initializes a new instance of the <see cref="DurableTaskScaleExtension"/> class.
        /// This constructor resolves the appropriate scalability provider factory
        /// and initializes a default scalability provider used for scaling decisions.
        /// </summary>
        /// <param name="metadata">The metadata for the Durable Task Scale extension.</param>
        /// <param name="logger">The logger instance used for diagnostic output.</param>
        /// <param name="scalabilityProviderFactories">A collection of available scalability provider factories.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when any of the required parameters (<paramref name="metadata"/>, <paramref name="logger"/>, or <paramref name="scalabilityProviderFactories"/>) are null.
        /// </exception>
        public DurableTaskScaleExtension(
            DurableTaskMetadata metadata,
            ILogger logger,
            IEnumerable<IScalabilityProviderFactory> scalabilityProviderFactories)
        {
            this.metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.scalabilityProviderFactories = scalabilityProviderFactories ?? throw new ArgumentNullException(nameof(scalabilityProviderFactories));

            // Determine which scalability provider factory should be used based on configured metadata.
            this.scalabilityProviderFactory = GetScalabilityProviderFactory(this.metadata, this.logger, this.scalabilityProviderFactories);

            // Create a default scalability provider instance from the selected factory.
            // For runtime-driven scaling, pass metadata with triggerMetadata = null (no Scale Controller properties needed)
            this.defaultscalabilityProvider = this.scalabilityProviderFactory.GetScalabilityProvider(this.metadata, triggerMetadata: null);
        }

        /// <summary>
        /// Gets the resolved <see cref="IScalabilityProviderFactory"/> instance.
        /// This factory is responsible for creating scalability providers based on the configured backend (e.g., Azure Storage, MSSQL, Netherite).
        /// </summary>
        public IScalabilityProviderFactory ScalabilityProviderFactory => this.scalabilityProviderFactory;

        /// <summary>
        /// Gets the default <see cref="ScalabilityProvider"/> instance created by the selected factory.
        /// This provider exposes methods to query current orchestration load and activity state for scaling decisions.
        /// </summary>
        public ScalabilityProvider DefaultScalabilityProvider => this.defaultscalabilityProvider;

        /// <summary>
        /// Inherited from IExtensionConfigProvider. Not used here.
        /// </summary>
        /// <param name="context">The extension configuration context provided by the WebJobs host.</param>
        public void Initialize(ExtensionConfigContext context)
        {
            // No initialization required for scale extension
        }

        /// <summary>
        /// Determines the scalability provider factory based on the given metadata.
        /// </summary>
        /// <param name="metadata">The metadata specifying the target storage provider and hub configuration.</param>
        /// <param name="logger">The logger instance for diagnostic messages.</param>
        /// <param name="scalabilityProviderFactories">A collection of available scalability provider factories.</param>
        /// <returns>The resolved <see cref="IScalabilityProviderFactory"/> suitable for the configured provider.</returns>
        internal static IScalabilityProviderFactory GetScalabilityProviderFactory(
            DurableTaskMetadata metadata,
            ILogger logger,
            IEnumerable<IScalabilityProviderFactory> scalabilityProviderFactories)
        {
            const string DefaultProvider = "AzureStorage";
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
    }
}
