// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
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

            // Early return if a different backend is explicitly configured (e.g., "azureManaged" or "mssql")
            // If StorageProvider is null or doesn't specify "type", we continue (Azure Storage is the default)
            if (optionsValue.StorageProvider != null
                && optionsValue.StorageProvider.TryGetValue("type", out object value)
                && value is string s
                && !string.Equals(s, this.Name, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            this.options = optionsValue;

            // Resolve default connection name directly from payload keys or fall back
            this.DefaultConnectionName = ResolveConnectionName(optionsValue.StorageProvider) ?? ConnectionStringNames.Storage;
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
            ILogger logger = this.loggerFactory.CreateLogger(LoggerName);

            // Validate Azure Storage specific options
            this.ValidateAzureStorageOptions();

            // Extract TokenCredential from triggerMetadata if present (for Managed Identity)
            var tokenCredential = ExtractTokenCredential(triggerMetadata, logger);

            // Use the connection name that was already resolved in the constructor
            // this.DefaultConnectionName was set via ResolveConnectionName(options.Value.StorageProvider)
            var storageAccountClientProvider = this.clientProviderFactory.GetClientProvider(
                this.DefaultConnectionName,
                tokenCredential);

            var provider = new AzureStorageScalabilityProvider(
                storageAccountClientProvider,
                this.DefaultConnectionName,
                logger);

            // Extract max concurrent values from trigger options (already built from metadata)
            provider.MaxConcurrentTaskOrchestrationWorkItems = this.options.MaxConcurrentOrchestratorFunctions ?? 10;
            provider.MaxConcurrentTaskActivityWorkItems = this.options.MaxConcurrentActivityFunctions ?? 10;

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

        private static string ResolveConnectionName(System.Collections.Generic.IDictionary<string, object> storageProvider)
        {
            if (storageProvider == null)
            {
                return null;
            }

            if (storageProvider.TryGetValue("connectionName", out object v1) && v1 is string s1 && !string.IsNullOrWhiteSpace(s1))
            {
                return s1;
            }

            if (storageProvider.TryGetValue("connectionStringName", out object v2) && v2 is string s2 && !string.IsNullOrWhiteSpace(s2))
            {
                return s2;
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
