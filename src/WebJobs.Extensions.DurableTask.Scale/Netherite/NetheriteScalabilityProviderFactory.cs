// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using DurableTask.Netherite;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

#nullable enable
namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Netherite
{
    /// <summary>
    /// Factory class responsible for creating instances of <see cref="NetheriteScalabilityProvider"/>.
    /// </summary>
    public class NetheriteScalabilityProviderFactory : IScalabilityProviderFactory
    {
        private const string LoggerName = "Triggers.DurableTask.Netherite";
        internal const string ProviderName = "Netherite";

        // Default connection names matching Netherite's NetheriteOrchestrationServiceSettings defaults
        internal const string DefaultStorageConnectionName = "AzureWebJobsStorage";
        internal const string DefaultEventHubsConnectionName = "EventHubsConnection";

        private readonly Dictionary<(string, string, string), NetheriteScalabilityProvider> cachedProviders = new Dictionary<(string, string, string), NetheriteScalabilityProvider>();
        private readonly IConfiguration configuration;
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetheriteScalabilityProviderFactory"/> class.
        /// </summary>
        /// <param name="configuration">
        /// The <see cref="IConfiguration"/> interface used to resolve connection strings and application settings.
        /// </param>
        /// <param name="loggerFactory">
        /// The <see cref="ILoggerFactory"/> used to create loggers for diagnostics.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any required argument is <see langword="null"/>.
        /// </exception>
        public NetheriteScalabilityProviderFactory(
            IConfiguration configuration,
            ILoggerFactory loggerFactory)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            this.logger = this.loggerFactory.CreateLogger(LoggerName);

            // Default connection name for Netherite (storage connection)
            this.DefaultConnectionName = DefaultStorageConnectionName;
        }

        /// <summary>
        /// Gets the logical name of this scalability provider type.
        /// </summary>
        public virtual string Name => ProviderName;

        /// <summary>
        /// Gets the default connection name configured for this factory.
        /// </summary>
        public string DefaultConnectionName { get; }

        /// <summary>
        /// Returns a default <see cref="ScalabilityProvider"/> instance.
        /// This method should never be called for Netherite provider as metadata is always required.
        /// </summary>
        /// <returns>A default <see cref="NetheriteScalabilityProvider"/> instance.</returns>
        /// <exception cref="NotImplementedException">Always throws as this method should not be called.</exception>
        public virtual ScalabilityProvider GetScalabilityProvider()
        {
            throw new NotImplementedException("Netherite provider requires metadata and should not use parameterless GetScalabilityProvider()");
        }

        /// <summary>
        /// Creates a <see cref="NetheriteScalabilityProvider"/> instance based on the provided pre-deserialized metadata.
        /// </summary>
        /// <param name="metadata">The pre-deserialized Durable Task metadata.</param>
        /// <param name="triggerMetadata">Trigger metadata used to access Properties like token credentials.</param>
        /// <returns>
        /// A <see cref="NetheriteScalabilityProvider"/> instance configured using
        /// the specified metadata and resolved connection information.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if no valid connection string could be resolved for the given connection name.
        /// </exception>
        public ScalabilityProvider GetScalabilityProvider(DurableTaskMetadata metadata, TriggerMetadata? triggerMetadata)
        {
            // Netherite requires two separate connections:
            // 1. Storage connection (for blobs/tables) - defaults to "AzureWebJobsStorage"
            // 2. Event Hubs connection - defaults to "EventHubsConnection"
            // These can be configured in host.json under extensions.durableTask.storageProvider:
            //   "StorageConnectionName": "AzureWebJobsStorage"
            //   "EventHubsConnectionName": "EventHubsConnection"
            // See: https://learn.microsoft.com/en-us/azure/azure-functions/durable/quickstart-netherite

            string storageConnectionName = ResolveStorageProviderProperty(metadata?.StorageProvider, "StorageConnectionName")
                ?? DefaultStorageConnectionName;
            string eventHubsConnectionName = ResolveStorageProviderProperty(metadata?.StorageProvider, "EventHubsConnectionName")
                ?? DefaultEventHubsConnectionName;

            this.logger.LogInformation("using storage connectionName" + storageConnectionName + " using eventhub connection" + eventHubsConnectionName);
            // Resolve the connection strings
            string resolvedStorageConnectionString = this.ResolveConnectionString(storageConnectionName);
            string resolvedEventHubsConnectionString = this.ResolveConnectionString(eventHubsConnectionName);

            // Extract task hub name from metadata
            string taskHubName = metadata?.TaskHubName ?? "default";

            // Cache key: (taskHub, storageConnection, eventHubsConnection)
            (string, string, string) cacheKey = (taskHubName, storageConnectionName, eventHubsConnectionName);

            lock (this.cachedProviders)
            {
                // If a provider has already been created for this configuration, return it.
                if (this.cachedProviders.TryGetValue(cacheKey, out NetheriteScalabilityProvider? cachedProvider))
                {
                    this.logger.LogDebug(
                        "Returning cached Netherite scalability provider for task hub '{TaskHub}', storage '{Storage}', eventHubs '{EventHubs}'",
                        taskHubName,
                        storageConnectionName,
                        eventHubsConnectionName);
                    return cachedProvider;
                }

                // Create Netherite orchestration service settings
                 var settings = new NetheriteOrchestrationServiceSettings
                {
                    HubName = taskHubName,
                    StorageConnectionName = storageConnectionName,
                    EventHubsConnectionName = eventHubsConnectionName,
                };

                // Set concurrency limits from metadata
                if (metadata?.MaxConcurrentOrchestratorFunctions.HasValue == true)
                {
                    settings.MaxConcurrentOrchestratorFunctions = metadata.MaxConcurrentOrchestratorFunctions.Value;
                }

                if (metadata?.MaxConcurrentActivityFunctions.HasValue == true)
                {
                    settings.MaxConcurrentActivityFunctions = metadata.MaxConcurrentActivityFunctions.Value;
                }

                // Create a connection resolver that Netherite uses to translate connection names
                // (e.g., "AzureWebJobsStorage") into actual connection strings.
                // This is called by settings.Validate() and internally by NetheriteOrchestrationService
                // when it connects to Event Hubs and Azure Storage.
                var connectionResolver = ConnectionResolver.FromConnectionNameToConnectionStringResolver(
                    (name) =>
                    {
                        if (string.Equals(name, storageConnectionName, StringComparison.OrdinalIgnoreCase))
                        {
                            return resolvedStorageConnectionString;
                        }
                        else if (string.Equals(name, eventHubsConnectionName, StringComparison.OrdinalIgnoreCase))
                        {
                            return resolvedEventHubsConnectionString;
                        }

                        // Fall back to direct lookup for any other connection names
                        return this.ResolveConnectionString(name);
                    });

                // Validate the settings
                settings.Validate(connectionResolver);

                this.logger.LogInformation(
                    "Creating Netherite scalability provider for task hub '{TaskHub}', storage '{Storage}', eventHubs '{EventHubs}'",
                    taskHubName,
                    storageConnectionName,
                    eventHubsConnectionName);

                // Create the Netherite orchestration service
                var service = new NetheriteOrchestrationService(settings, this.loggerFactory, serviceProvider: null);

                // Create our scalability provider - use storage connection name as the primary connection name
                var provider = new NetheriteScalabilityProvider(service, settings, storageConnectionName, this.logger);

                // Set max concurrent values from metadata
                // Default: 10 times the number of processors on the current machine
                provider.MaxConcurrentTaskOrchestrationWorkItems = metadata?.MaxConcurrentOrchestratorFunctions ?? (Environment.ProcessorCount * 10);
                provider.MaxConcurrentTaskActivityWorkItems = metadata?.MaxConcurrentActivityFunctions ?? (Environment.ProcessorCount * 10);

                this.cachedProviders.Add(cacheKey, provider);
                return provider;
            }
        }

        /// <summary>
        /// Extracts a property value from the storage provider configuration dictionary.
        /// Performs case-insensitive property name lookup.
        /// </summary>
        private static string? ResolveStorageProviderProperty(IDictionary<string, object>? storageProvider, string propertyName)
        {
            if (storageProvider == null)
            {
                return null;
            }

            // Try exact match first
            if (storageProvider.TryGetValue(propertyName, out object? value) && value is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
            {
                return stringValue;
            }

            // Fall back to case-insensitive search
            foreach (var kvp in storageProvider)
            {
                if (string.Equals(kvp.Key, propertyName, StringComparison.OrdinalIgnoreCase) && kvp.Value is string strValue && !string.IsNullOrWhiteSpace(strValue))
                {
                    return strValue;
                }
            }

            return null;
        }

        private string ResolveConnectionString(string connectionName)
        {
            // Look up connection string from configuration
            // Note: Scale Controller already resolves %xxx% wrapping before calling the extension
            string? connectionString =
                this.configuration.GetConnectionString(connectionName) ??
                this.configuration[connectionName] ??
                Environment.GetEnvironmentVariable(connectionName);

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    $"No valid connection string found for '{connectionName}'. " +
                    $"Please ensure it is defined in app settings, connection strings, or environment variables.");
            }

            return connectionString;
        }
    }
}
