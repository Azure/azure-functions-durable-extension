// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using Azure.Core;
using DurableTask.Netherite;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

#nullable enable
namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Netherite
{
    /// <summary>
    /// Factory class responsible for creating and managing instances of <see cref="NetheriteScalabilityProvider"/>.
    /// </summary>
    public class NetheriteScalabilityProviderFactory : IScalabilityProviderFactory
    {
        private const string LoggerName = "Triggers.DurableTask.Netherite";
        internal const string ProviderName = "Netherite";

        /// <summary>
        /// The key used to retrieve the Event Hubs token credential function pointer from TriggerMetadata.Properties.
        /// This is used for Scale Controller identity support.
        /// </summary>
        internal const string GetNetheriteEventHubsTokenCredential = "GetNetheriteEventHubsTokenCredential";

        private readonly Dictionary<(string, string?), NetheriteScalabilityProvider> cachedProviders = new Dictionary<(string, string?), NetheriteScalabilityProvider>();
        private readonly IConfiguration configuration;
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger logger;
        private readonly IServiceProvider? serviceProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetheriteScalabilityProviderFactory"/> class.
        /// </summary>
        /// <param name="configuration">
        /// The <see cref="IConfiguration"/> interface used to resolve connection strings and application settings.
        /// </param>
        /// <param name="loggerFactory">
        /// The <see cref="ILoggerFactory"/> used to create loggers for diagnostics.
        /// </param>
        /// <param name="serviceProvider">
        /// Optional. The <see cref="IServiceProvider"/> used for runtime scaling to resolve
        /// AzureComponentFactory for identity-based authentication. Pass null for Scale Controller scenarios.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any required argument is <see langword="null"/>.
        /// </exception>
        public NetheriteScalabilityProviderFactory(
            IConfiguration configuration,
            ILoggerFactory loggerFactory,
            IServiceProvider? serviceProvider = null)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            this.serviceProvider = serviceProvider;
            this.logger = this.loggerFactory.CreateLogger(LoggerName);

            // Default connection name format: "StorageConnectionName,EventHubsConnectionName"
            this.DefaultConnectionName = "Storage,EventHubsConnection";
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
        /// Returns a default <see cref="ScalabilityProvider"/> instance configured with the default connection and global scaling options.
        /// This method should never be called for Netherite provider as metadata is always required.
        /// </summary>
        /// <returns> A default <see cref="NetheriteScalabilityProvider"/> instance.</returns>
        /// <exception cref="NotImplementedException">Always throws as this method should not be called.</exception>
        public virtual ScalabilityProvider GetScalabilityProvider()
        {
            throw new NotImplementedException("Netherite provider requires metadata and should not use parameterless GetScalabilityProvider()");
        }

        /// <summary>
        /// Creates or retrieves a <see cref="NetheriteScalabilityProvider"/> instance based on the provided pre-deserialized metadata.
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
            // Resolve connection name: prioritize metadata, fallback to default
            string? rawConnectionName = TriggerMetadataExtensions.ResolveConnectionName(metadata?.StorageProvider);
            string connectionName = rawConnectionName ?? this.DefaultConnectionName;

            // For Netherite, the connection name can be:
            // 1. A comma-separated pair: "StorageConnectionName,EventHubsConnectionName"
            // 2. A single connection name that will be used for both storage and event hubs
            string storageConnectionName;
            string eventHubsConnectionName;

            if (connectionName.Contains(","))
            {
                var parts = connectionName.Split(',');
                if (parts.Length != 2)
                {
                    throw new InvalidOperationException(
                        $"Invalid Netherite connection name format: '{connectionName}'. " +
                        $"Expected format: 'StorageConnectionName,EventHubsConnectionName'");
                }

                storageConnectionName = parts[0].Trim();
                eventHubsConnectionName = parts[1].Trim();
            }
            else
            {
                // Use the same connection name for both
                storageConnectionName = connectionName;
                eventHubsConnectionName = connectionName;
            }

            // Resolve the connection strings
            string resolvedStorageConnectionString = this.ResolveConnectionString(storageConnectionName);
            string resolvedEventHubsConnectionString = this.ResolveConnectionString(eventHubsConnectionName);

            // Extract task hub name from metadata
            string taskHubName = metadata?.TaskHubName ?? "default";

            // Include task hub name in cache key
            (string, string?) cacheKey = (connectionName, taskHubName);

            this.logger.LogDebug(
                "Getting durability provider for connection '{Connection}' and task hub '{TaskHub}'...",
                cacheKey.Item1,
                cacheKey.Item2 ?? "null");

            lock (this.cachedProviders)
            {
                // If a provider has already been created for this connection name and task hub, return it.
                if (this.cachedProviders.TryGetValue(cacheKey, out NetheriteScalabilityProvider? cachedProvider))
                {
                    this.logger.LogDebug(
                        "Returning cached durability provider for connection '{Connection}' and task hub '{TaskHub}'",
                        cacheKey.Item1,
                        cacheKey.Item2);
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

                // Create a simple connection resolver that returns the resolved connection strings
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

                        // Fall back to configuration lookup for any other connection names
                        return this.configuration.GetConnectionString(name) ??
                               this.configuration[name] ??
                               Environment.GetEnvironmentVariable(name);
                    });

                // Validate the settings
                settings.Validate(connectionResolver);

                this.logger.LogInformation(
                    "Creating durability provider for connection '{Connection}' and task hub '{TaskHub}'...",
                    cacheKey.Item1,
                    cacheKey.Item2);

                // Determine the service provider to use for identity-based authentication
                // For runtime scaling (host), use the injected serviceProvider which has AzureComponentFactory
                // For Scale Controller, create a wrapper from TriggerMetadata.Properties
                IServiceProvider? effectiveServiceProvider = this.serviceProvider;

                if (effectiveServiceProvider == null && triggerMetadata?.Properties != null)
                {
                    // Scale Controller path: try to get credentials from TriggerMetadata.Properties
                    AzureComponentFactory? componentFactory = null;
                    Func<string, TokenCredential>? eventHubsCredentialFunc = null;

                    // Get AzureComponentFactory for Storage identity
                    if (triggerMetadata.Properties.TryGetValue(nameof(AzureComponentFactory), out object? factoryObj) &&
                        factoryObj is AzureComponentFactory factory)
                    {
                        componentFactory = factory;
                        this.logger.LogInformation("Retrieved AzureComponentFactory from trigger metadata for Storage identity.");
                    }

                    // Get Event Hubs credential function pointer
                    if (triggerMetadata.Properties.TryGetValue(GetNetheriteEventHubsTokenCredential, out object? credentialFuncObj) &&
                        credentialFuncObj is Func<string, TokenCredential> credentialFunc)
                    {
                        eventHubsCredentialFunc = credentialFunc;
                        this.logger.LogInformation("Retrieved Event Hubs credential function from trigger metadata for Netherite identity.");
                    }

                    // Create a wrapper service provider if we have any credentials
                    if (componentFactory != null || eventHubsCredentialFunc != null)
                    {
                        effectiveServiceProvider = new NetheriteScaleControllerServiceProvider(
                            componentFactory,
                            eventHubsCredentialFunc,
                            eventHubsConnectionName,
                            this.logger);
                    }
                }

                // Create our scalability provider
                var provider = new NetheriteScalabilityProvider(settings, connectionName, this.logger);

                // Extract max concurrent values from trigger metadata (from Scale Controller payload)
                // Default: 10 times the number of processors on the current machine
                provider.MaxConcurrentTaskOrchestrationWorkItems = metadata?.MaxConcurrentOrchestratorFunctions ?? (Environment.ProcessorCount * 10);
                provider.MaxConcurrentTaskActivityWorkItems = metadata?.MaxConcurrentActivityFunctions ?? (Environment.ProcessorCount * 10);

                this.cachedProviders.Add(cacheKey, provider);
                return provider;
            }
        }

        private string ResolveConnectionString(string connectionName)
        {
            string? connectionString = null;

            connectionString =
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
