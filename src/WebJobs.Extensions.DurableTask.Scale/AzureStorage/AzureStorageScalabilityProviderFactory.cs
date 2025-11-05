// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Linq;
using System.Text.Json;
using DurableTask.AzureStorage;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.AzureStorage
{
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
            // Validate arguments first
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (clientProviderFactory == null)
            {
                throw new ArgumentNullException(nameof(clientProviderFactory));
            }

            if (nameResolver == null)
            {
                throw new ArgumentNullException(nameof(nameResolver));
            }

            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            // this constructor may be called by dependency injection even if the AzureStorage provider is not selected
            // in that case, return immediately, since this provider is not actually used, but can still throw validation errors
            if (options.Value.StorageProvider != null 
                && options.Value.StorageProvider.TryGetValue("type", out object value)
                && value is string s
                && !string.Equals(s, this.Name, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            this.options = options.Value;
            this.clientProviderFactory = clientProviderFactory;
            this.nameResolver = nameResolver;
            this.loggerFactory = loggerFactory;

            // Resolve default connection name directly from payload keys or fall back
            this.DefaultConnectionName = ResolveConnectionName(options.Value.StorageProvider) ?? ConnectionStringNames.Storage;
        }

        public virtual string Name => ProviderName;

        public string DefaultConnectionName { get; }

        public virtual ScalabilityProvider GetDurabilityProvider()
        {
            if (this.defaultStorageProvider == null)
            {
                ILogger logger = this.loggerFactory.CreateLogger(LoggerName);

                // Validate Azure Storage specific options
                this.ValidateAzureStorageOptions(logger);

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

        public ScalabilityProvider GetDurabilityProvider(TriggerMetadata triggerMetadata)
        {
            ILogger logger = this.loggerFactory.CreateLogger(LoggerName);

            // Validate Azure Storage specific options
            this.ValidateAzureStorageOptions(logger);

            // Get the pre-parsed metadata from triggerMetadata.Properties (parsed by DurableTaskTriggersScaleProvider)
            DurableTaskMetadata parsedMetadata = ExtractParsedMetadata(triggerMetadata);

            // Extract TokenCredential from triggerMetadata if present (for Managed Identity)
            var tokenCredential = ExtractTokenCredential(triggerMetadata);

            // Use the connection name that was already resolved in the constructor
            // this.DefaultConnectionName was set via ResolveConnectionName(options.Value.StorageProvider)
            var storageAccountClientProvider = this.clientProviderFactory.GetClientProvider(
                this.DefaultConnectionName,
                tokenCredential);

            var provider = new AzureStorageScalabilityProvider(
                storageAccountClientProvider,
                this.DefaultConnectionName,
                logger);

            // Extract max concurrent values from parsed metadata first, fallback to DI options
            provider.MaxConcurrentTaskOrchestrationWorkItems = parsedMetadata?.MaxConcurrentOrchestratorFunctions 
                ?? this.options.MaxConcurrentOrchestratorFunctions 
                ?? 10;
            provider.MaxConcurrentTaskActivityWorkItems = parsedMetadata?.MaxConcurrentActivityFunctions 
                ?? this.options.MaxConcurrentActivityFunctions 
                ?? 10;

            return provider;
        }

        private static global::Azure.Core.TokenCredential ExtractTokenCredential(TriggerMetadata triggerMetadata)
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
                    catch (Exception)
                    {
                        // Failed to extract credential, return null
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
        private void ValidateAzureStorageOptions(ILogger logger)
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

        private static DurableTaskMetadata ExtractParsedMetadata(TriggerMetadata triggerMetadata)
        {
            if (triggerMetadata?.Properties == null)
            {
                return null;
            }

            // The DurableTaskTriggersScaleProvider pre-parses the metadata and stores it in Properties
            if (triggerMetadata.Properties.TryGetValue("DurableTaskMetadata", out object metadataObj) 
                && metadataObj is DurableTaskMetadata metadata)
            {
                return metadata;
            }

            return null;
        }
    }
}
