// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using Azure.Core;
using DurableTask.Netherite;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

#nullable enable
namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Netherite
{
    /// <summary>
    /// Factory class responsible for creating and managing instances of <see cref="NetheriteScalabilityProvider"/>.
    /// </summary>
    public class NetheriteScalabilityProviderFactory : IScalabilityProviderFactory
    {
        internal const string ProviderName = "Netherite";
        internal const string GetNetheriteEventHubsTokenCredential = "GetNetheriteEventHubsTokenCredential";

        private const string LoggerName = "Triggers.DurableTask.Netherite";

        private readonly Dictionary<(string, string?), NetheriteScalabilityProvider> cachedProviders = new Dictionary<(string, string?), NetheriteScalabilityProvider>();
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
        /// Creates or retrieves a <see cref="NetheriteScalabilityProvider"/> instance based on the provided pre-deserialized metadata.
        /// </summary>
        /// <param name="metadata">The pre-deserialized Durable Task metadata.</param>
        /// <param name="triggerMetadata">Trigger metadata used to access Properties like token credentials.</param>
        /// <returns>
        /// A <see cref="NetheriteScalabilityProvider"/> instance configured using
        /// the specified metadata and resolved connection information.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if no valid connection could be resolved for the given connection name.
        /// </exception>
        public ScalabilityProvider GetScalabilityProvider(DurableTaskMetadata metadata, TriggerMetadata? triggerMetadata)
        {
            // Resolve connection name: prioritize metadata, fallback to default
            string? rawConnectionName = TriggerMetadataExtensions.ResolveConnectionName(metadata?.StorageProvider);
            string connectionName = rawConnectionName ?? this.DefaultConnectionName;
            this.logger.LogInformation("Using connection name '{ConnectionName}'", connectionName);

            // For Netherite, the connection name can be:
            // 1. A comma-separated pair: "StorageConnectionName,EventHubsConnectionName"
            // 2. A single connection name used for both storage and event hubs
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
                storageConnectionName = connectionName;
                eventHubsConnectionName = connectionName;
            }

            // Extract task hub name from metadata
            string taskHubName = metadata?.TaskHubName ?? "default";

            (string, string?) cacheKey = (connectionName, taskHubName);

            this.logger.LogDebug(
                "Getting durability provider for connection '{Connection}' and task hub '{TaskHub}'...",
                cacheKey.Item1,
                cacheKey.Item2 ?? "null");

            int defaultConcurrency = 10;
            int maxConcurrentOrchestrators = metadata?.MaxConcurrentOrchestratorFunctions ?? defaultConcurrency;
            int maxConcurrentActivities = metadata?.MaxConcurrentActivityFunctions ?? defaultConcurrency;

            lock (this.cachedProviders)
            {
                // If a provider has already been created for this connection name and task hub,
                // return it only if concurrency settings haven't changed. Otherwise evict and
                // recreate so the underlying service picks up the new limits.
                if (this.cachedProviders.TryGetValue(cacheKey, out NetheriteScalabilityProvider? cachedProvider))
                {
                    // Netherite provider only needs orchestration and activity functions concurrency.
                    bool concurrencyChanged =
                        cachedProvider.MaxConcurrentTaskOrchestrationWorkItems != maxConcurrentOrchestrators ||
                        cachedProvider.MaxConcurrentTaskActivityWorkItems != maxConcurrentActivities;

                    if (!concurrencyChanged)
                    {
                        this.logger.LogDebug(
                            "Returning cached durability provider for connection '{Connection}' and task hub '{TaskHub}'",
                            cacheKey.Item1,
                            cacheKey.Item2);
                        return cachedProvider;
                    }

                    this.logger.LogInformation(
                        "Concurrency settings changed for connection '{Connection}', task hub '{TaskHub}'. Recreating provider.",
                        cacheKey.Item1,
                        cacheKey.Item2);
                    this.cachedProviders.Remove(cacheKey);
                }

                // Extract token credentials from TriggerMetadata (Scale Controller path).
                TokenCredential? storageTokenCredential = TriggerMetadataExtensions.ExtractTokenCredential(triggerMetadata, this.logger);
                TokenCredential? eventHubsTokenCredential = ExtractEventHubsTokenCredential(triggerMetadata, eventHubsConnectionName, this.logger);

                // Build a connection resolver that supports both connection strings and identity-based auth.
                var connectionResolver = new NetheriteScaleControllerConnectionResolver(
                    this.configuration,
                    storageTokenCredential,
                    eventHubsTokenCredential,
                    this.logger);

                var settings = new NetheriteOrchestrationServiceSettings
                {
                    HubName = taskHubName,
                    StorageConnectionName = storageConnectionName,
                    EventHubsConnectionName = eventHubsConnectionName,
                    MaxConcurrentOrchestratorFunctions = maxConcurrentOrchestrators,
                    MaxConcurrentActivityFunctions = maxConcurrentActivities,
                };

                // Validate invokes connectionResolver methods in this order:
                // 1. ResolveLayerConfiguration(EventHubsConnectionName) -> determines Faster + EventHubs
                // 2. ResolveConnectionInfo(hubName, EventHubsConnectionName, EventHubsNamespace) -> settings.EventHubsConnection
                // 3. ResolveConnectionInfo(hubName, StorageConnectionName, BlobStorage) -> settings.BlobStorageConnection
                // 4. ResolveConnectionInfo(hubName, StorageConnectionName, TableStorage) -> settings.TableStorageConnection (if configured)
                // See class NetheriteScaleControllerConnectionResolver for full details.
                settings.Validate(connectionResolver);

                this.logger.LogInformation(
                    "Creating durability provider for connection '{Connection}' and task hub '{TaskHub}'...",
                    cacheKey.Item1,
                    cacheKey.Item2);

                var provider = new NetheriteScalabilityProvider(settings, connectionName, this.logger);
                provider.MaxConcurrentTaskOrchestrationWorkItems = maxConcurrentOrchestrators;
                provider.MaxConcurrentTaskActivityWorkItems = maxConcurrentActivities;

                this.cachedProviders.Add(cacheKey, provider);
                return provider;
            }
        }

        /// <summary>
        /// Extracts an Event Hubs-specific TokenCredential from TriggerMetadata (Scale Controller path).
        /// This allows using a different identity for Event Hubs than for Storage.
        /// Falls back to null if no Event Hubs-specific credential is available;
        /// the connection resolver will then fall back to the storage credential.
        /// </summary>
        private static TokenCredential? ExtractEventHubsTokenCredential(TriggerMetadata? triggerMetadata, string connectionName, ILogger? logger)
        {
            if (triggerMetadata?.Properties == null)
            {
                return null;
            }

            if (triggerMetadata.Properties.TryGetValue(GetNetheriteEventHubsTokenCredential, out object? credentialFuncObj) &&
                credentialFuncObj is Func<string, TokenCredential> credentialFunc)
            {
                try
                {
                    TokenCredential? credential = credentialFunc(connectionName);
                    if (credential != null)
                    {
                        logger?.LogInformation(
                            "Retrieved Event Hubs token credential from trigger metadata for connection '{Connection}'.",
                            connectionName);
                        return credential;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(
                        ex,
                        "Failed to retrieve Event Hubs token credential from trigger metadata for connection '{Connection}'.",
                        connectionName);
                }
            }

            return null;
        }
    }
}
