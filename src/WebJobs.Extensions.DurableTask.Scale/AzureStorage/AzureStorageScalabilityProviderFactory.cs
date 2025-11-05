// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.AzureStorage
{
    /// <summary>
    /// Factory class responsible for creating <see cref="AzureStorageScalabilityProvider"/> instances.
    /// </summary>
    public class AzureStorageScalabilityProviderFactory : IScalabilityProviderFactory
    {
        private const string LoggerName = "Host.Triggers.DurableTask.AzureStorage";
        internal const string ProviderName = "AzureStorage";

        private readonly DurableTaskScaleOptions options;
        private readonly IStorageServiceClientProviderFactory clientProviderFactory;
        private readonly INameResolver nameResolver;
        private readonly ILoggerFactory loggerFactory;
        private AzureStorageScalabilityProvider defaultStorageProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureStorageScalabilityProviderFactory"/> class.
        /// </summary>
        /// <param name="options">The durable task scale options.</param>
        /// <param name="clientProviderFactory">The storage client provider factory.</param>
        /// <param name="nameResolver">The name resolver for connection strings.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
        public AzureStorageScalabilityProviderFactory(
            IOptions<DurableTaskScaleOptions> options,
            IStorageServiceClientProviderFactory clientProviderFactory,
            INameResolver nameResolver,
            ILoggerFactory loggerFactory)
        {
            this.clientProviderFactory = clientProviderFactory ?? throw new ArgumentNullException(nameof(clientProviderFactory));
            this.nameResolver = nameResolver ?? throw new ArgumentNullException(nameof(nameResolver));
            this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

            var optionsValue = options?.Value ?? throw new ArgumentNullException(nameof(options));

            // In Scale Controller context, optionsValue will be a default/empty object (can't read host.json)
            // The real configuration comes from triggerMetadata in GetScalabilityProvider()
            this.options = optionsValue;

            // Resolve default connection name directly from payload keys or fall back
            this.DefaultConnectionName = "AzureWebJobsStorage";
        }

        /// <summary>
        /// Name of this provider service.
        /// </summary>
        public virtual string Name => ProviderName;

        /// <summary>
        /// Default connection name of this provider service.
        /// </summary>
        public string DefaultConnectionName { get; }

        /// <summary>
        /// Creates and caches a default <see cref="ScalabilityProvider"/> instanceusing Azure Storage as the backend.
        /// </summary>
        /// <returns>
        /// A singleton instance of <see cref="AzureStorageScalabilityProvider"/>.
        /// </returns>
        public virtual ScalabilityProvider GetScalabilityProvider()
        {
            if (this.defaultStorageProvider == null)
            {
                ILogger logger = this.loggerFactory.CreateLogger(LoggerName);

                // Validate Azure Storage specific options
                this.ValidateAzureStorageOptions();

                // Create StorageAccountClientProvider without credential (connection string)
                var storageAccountClientProvider = this.clientProviderFactory.GetClientProvider(
                    this.DefaultConnectionName,
                    tokenCredential: null);

                this.defaultStorageProvider = new AzureStorageScalabilityProvider(
                    storageAccountClientProvider,
                    this.DefaultConnectionName,
                    logger);

                // Set the max concurrent values from options
                this.defaultStorageProvider.MaxConcurrentTaskOrchestrationWorkItems = this.options.MaxConcurrentOrchestratorFunctions ?? 10;
                this.defaultStorageProvider.MaxConcurrentTaskActivityWorkItems = this.options.MaxConcurrentActivityFunctions ?? 10;
            }

            return this.defaultStorageProvider;
        }

        /// <summary>
        /// Creates and caches a default <see cref="ScalabilityProvider"/> instanceusing Azure Storage as the backend using
        /// connection and credential information extracted from the given <paramref name="triggerMetadata"/>.
        /// </summary>
        /// <returns>
        /// A singleton instance of <see cref="AzureStorageScalabilityProvider"/>.
        /// </returns>
        public ScalabilityProvider GetScalabilityProvider(TriggerMetadata triggerMetadata)
        {
            // Extract options from triggerMetadata (sent by Functions Host in SyncTriggers payload)
            // This is critical for Scale Controller which doesn't have access to host.json
            DurableTaskScaleOptions? triggerOptions = triggerMetadata.ExtractDurableTaskScaleOptions();

            ILogger logger = this.loggerFactory.CreateLogger(LoggerName);

            // Validate Azure Storage specific options
            this.ValidateAzureStorageOptions();

            // Extract TokenCredential from triggerMetadata if present (for Managed Identity)
            var tokenCredential = ExtractTokenCredential(triggerMetadata, logger);

            // Resolve connection name: prioritize triggerOptions, fallback to default
            string? rawConnectionName = TriggerMetadataExtensions.ResolveConnectionName(triggerOptions?.StorageProvider);
            string connectionName = rawConnectionName != null
                ? this.nameResolver.Resolve(rawConnectionName)
                : this.DefaultConnectionName;

            var storageAccountClientProvider = this.clientProviderFactory.GetClientProvider(
                connectionName,
                tokenCredential);

            var provider = new AzureStorageScalabilityProvider(
                storageAccountClientProvider,
                connectionName,
                logger);

            // Extract max concurrent values from trigger metadata (from Scale Controller payload)
            provider.MaxConcurrentTaskOrchestrationWorkItems = triggerOptions?.MaxConcurrentOrchestratorFunctions ?? this.options.MaxConcurrentOrchestratorFunctions ?? 10;
            provider.MaxConcurrentTaskActivityWorkItems = triggerOptions?.MaxConcurrentActivityFunctions ?? this.options.MaxConcurrentActivityFunctions ?? 10;

            return provider;
        }

        // Scale Controller will return a AzureComponentWrapper which might contain a token crednetial to use.
        private static global::Azure.Core.TokenCredential ExtractTokenCredential(TriggerMetadata triggerMetadata, ILogger logger)
        {
            if (triggerMetadata?.Properties == null)
            {
                return null;
            }

            // Check if metadata contains an AzureComponentFactory wrapper
            // ScaleController passes it as: metadata.Properties[nameof(AzureComponentFactory)] = new AzureComponentFactoryWrapper(...)
            if (triggerMetadata.Properties.TryGetValue("AzureComponentFactory", out object componentFactoryObj) && componentFactoryObj != null)
            {
                // The AzureComponentFactoryWrapper has CreateTokenCredential method
                // Call it using reflection to get the TokenCredential
                var factoryType = componentFactoryObj.GetType();
                var method = factoryType.GetMethod("CreateTokenCredential");
                if (method != null)
                {
                    try
                    {
                        // Call CreateTokenCredential(null) to get the TokenCredential from the wrapper
                        var credential = method.Invoke(componentFactoryObj, new object[] { null });
                        if (credential is global::Azure.Core.TokenCredential tokenCredential)
                        {
                            return tokenCredential;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Failed to extract TokenCredential from AzureComponentFactory. Using null credential instead.");
                        return null;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Validates Azure Storage specific options.
        /// </summary>
        private void ValidateAzureStorageOptions()
        {
            const int MinTaskHubNameSize = 3;
            const int MaxTaskHubNameSize = 50;

            // Validate hub name for Azure Storage
            if (!string.IsNullOrWhiteSpace(this.options.HubName))
            {
                var hubName = this.options.HubName;

                if (hubName.Length < MinTaskHubNameSize || hubName.Length > MaxTaskHubNameSize)
                {
                    throw new System.ArgumentException($"Task hub name '{hubName}' should contain only alphanumeric characters, start with a letter, and have length between {MinTaskHubNameSize} and {MaxTaskHubNameSize}.");
                }

                // Must start with a letter
                if (!char.IsLetter(hubName[0]))
                {
                    throw new System.ArgumentException($"Task hub name '{hubName}' should contain only alphanumeric characters, start with a letter, and have length between {MinTaskHubNameSize} and {MaxTaskHubNameSize}.");
                }

                // Must contain only alphanumeric characters
                if (!hubName.All(char.IsLetterOrDigit))
                {
                    throw new System.ArgumentException($"Task hub name '{hubName}' should contain only alphanumeric characters, start with a letter, and have length between {MinTaskHubNameSize} and {MaxTaskHubNameSize}.");
                }
            }

            // Validate max concurrent orchestrator functions
            if (this.options.MaxConcurrentOrchestratorFunctions.HasValue && this.options.MaxConcurrentOrchestratorFunctions.Value <= 0)
            {
                throw new System.InvalidOperationException($"{nameof(this.options.MaxConcurrentOrchestratorFunctions)} must be a positive integer.");
            }

            // Validate max concurrent activity functions
            if (this.options.MaxConcurrentActivityFunctions.HasValue && this.options.MaxConcurrentActivityFunctions.Value <= 0)
            {
                throw new System.InvalidOperationException($"{nameof(this.options.MaxConcurrentActivityFunctions)} must be a positive integer.");
            }
        }
    }
}
