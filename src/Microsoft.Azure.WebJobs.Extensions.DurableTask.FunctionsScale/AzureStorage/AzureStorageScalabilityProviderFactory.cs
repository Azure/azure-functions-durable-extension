// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable

using System;
using System.Linq;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.AzureStorage
{
    /// <summary>
    /// Factory class responsible for creating <see cref="AzureStorageScalabilityProvider"/> instances.
    /// </summary>
    public class AzureStorageScalabilityProviderFactory : IScalabilityProviderFactory
    {
        internal const string ProviderName = "AzureStorage";
        private const string LoggerName = "Triggers.DurableTask.AzureStorage";

        private readonly IStorageServiceClientProviderFactory clientProviderFactory;
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureStorageScalabilityProviderFactory"/> class.
        /// </summary>
        /// <param name="clientProviderFactory">The storage client provider factory.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
        public AzureStorageScalabilityProviderFactory(
            IStorageServiceClientProviderFactory clientProviderFactory,
            ILoggerFactory loggerFactory)
        {
            this.clientProviderFactory = clientProviderFactory ?? throw new ArgumentNullException(nameof(clientProviderFactory));
            this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            this.logger = this.loggerFactory.CreateLogger(LoggerName);

            // Default connection name for Azure Storage
            this.DefaultConnectionName = "AzureWebJobsStorage";
        }

        /// <summary>
        /// Gets name of this provider service.
        /// </summary>
        public virtual string Name => ProviderName;

        /// <summary>
        /// Gets default connection name of this provider service.
        /// </summary>
        public string DefaultConnectionName { get; }

        /// <summary>
        /// Creates a default <see cref="ScalabilityProvider"/> instance using Azure Storage as the backend,
        /// using the provided pre-deserialized metadata and trigger metadata for accessing properties.
        /// </summary>
        /// <param name="metadata">The pre-deserialized Durable Task metadata.</param>
        /// <param name="triggerMetadata">Trigger metadata used to access properties like token credentials.</param>
        /// <returns>
        /// A newly created instance of <see cref="AzureStorageScalabilityProvider"/>.
        /// </returns>
        public ScalabilityProvider GetScalabilityProvider(DurableTaskMetadata metadata, TriggerMetadata? triggerMetadata)
        {
            // Validate Azure Storage specific options if metadata is present
            if (metadata != null)
            {
                this.ValidateAzureStorageMetadata(metadata);
            }

            // Extract TokenCredential from triggerMetadata if present (for Managed Identity)
            var tokenCredential = TriggerMetadataExtensions.ExtractTokenCredential(triggerMetadata, this.logger);

            // Get connection name from metadata, fallback to default
            string? rawConnectionName = TriggerMetadataExtensions.ResolveConnectionName(metadata?.StorageProvider);
            string connectionName = rawConnectionName ?? this.DefaultConnectionName;

            var storageAccountClientProvider = this.clientProviderFactory.GetClientProvider(
                connectionName,
                tokenCredential);

            var provider = new AzureStorageScalabilityProvider(
                storageAccountClientProvider,
                connectionName,
                this.logger);

            // Extract max concurrent values from metadata
            // Azure storage backend scaling doesn't need entity concurrency settings.
            // Default: 10
            provider.MaxConcurrentTaskOrchestrationWorkItems = metadata?.MaxConcurrentOrchestratorFunctions ?? 10;
            provider.MaxConcurrentTaskActivityWorkItems = metadata?.MaxConcurrentActivityFunctions ?? 10;

            return provider;
        }

        /// <summary>
        /// Validates Azure Storage specific metadata.
        /// </summary>
        private void ValidateAzureStorageMetadata(DurableTaskMetadata metadata)
        {
            const int MinTaskHubNameSize = 3;
            const int MaxTaskHubNameSize = 50;

            // Validate hub name for Azure Storage
            if (!string.IsNullOrWhiteSpace(metadata.TaskHubName))
            {
                var hubName = metadata.TaskHubName;

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
            if (metadata.MaxConcurrentOrchestratorFunctions.HasValue && metadata.MaxConcurrentOrchestratorFunctions.Value <= 0)
            {
                throw new System.InvalidOperationException($"{nameof(metadata.MaxConcurrentOrchestratorFunctions)} must be a positive integer.");
            }

            // Validate max concurrent activity functions
            if (metadata.MaxConcurrentActivityFunctions.HasValue && metadata.MaxConcurrentActivityFunctions.Value <= 0)
            {
                throw new System.InvalidOperationException($"{nameof(metadata.MaxConcurrentActivityFunctions)} must be a positive integer.");
            }

            if (metadata.MaxConcurrentEntityFunctions.HasValue && metadata.MaxConcurrentEntityFunctions.Value <= 0)
            {
                throw new System.InvalidOperationException($"{nameof(metadata.MaxConcurrentEntityFunctions)} must be a positive integer.");
            }
        }
    }
}
