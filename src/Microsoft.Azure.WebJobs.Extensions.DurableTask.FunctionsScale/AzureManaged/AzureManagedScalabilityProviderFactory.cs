// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using Azure.Core;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.DurableTask.AzureManagedBackend;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

#nullable enable
namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.AzureManaged
{
    /// <summary>
    /// Factory class responsible for creating and managing instances of <see cref="AzureManagedScalabilityProvider"/>.
    /// </summary>
    public class AzureManagedScalabilityProviderFactory : IScalabilityProviderFactory
    {
        private const string LoggerName = "Triggers.DurableTask.AzureManaged";
        private const string ProviderName = "AzureManaged";

        private readonly Dictionary<(string, string?, string?), AzureManagedScalabilityProvider> cachedProviders = new Dictionary<(string, string?, string?), AzureManagedScalabilityProvider>();
        private readonly IConfiguration configuration;
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureManagedScalabilityProviderFactory"/> class.
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
        public AzureManagedScalabilityProviderFactory(
            IConfiguration configuration,
            ILoggerFactory loggerFactory)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            this.logger = this.loggerFactory.CreateLogger(LoggerName);

            this.DefaultConnectionName = "DURABLE_TASK_SCHEDULER_CONNECTION_STRING";
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
        /// Creates or retrieves an <see cref="AzureManagedScalabilityProvider"/> instance based on the provided pre-deserialized metadata.
        /// </summary>
        /// <param name="metadata">The pre-deserialized Durable Task metadata.</param>
        /// <param name="triggerMetadata">Trigger metadata used to access Properties like token credentials.</param>
        /// <returns>
        /// An <see cref="AzureManagedScalabilityProvider"/> instance configured using
        /// the specified metadata and resolved connection information.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if no valid connection string could be resolved for the given connection name.
        /// </exception>
        public ScalabilityProvider GetScalabilityProvider(DurableTaskMetadata metadata, TriggerMetadata? triggerMetadata)
        {
            // Get connection name from metadata, fallback to default
            string? rawConnectionName = TriggerMetadataExtensions.ResolveConnectionName(metadata?.StorageProvider);
            string connectionName = rawConnectionName ?? this.DefaultConnectionName;
            this.logger.LogInformation("using connectionName" + connectionName);

            // Look up connection string from configuration
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

            AzureManagedConnectionString azureManagedConnectionString = new AzureManagedConnectionString(connectionString);

            // Extract task hub name from metadata
            string? taskHubName = metadata?.TaskHubName ?? azureManagedConnectionString.TaskHubName;

            // Include client ID in cache key to handle managed identity changes
            // Use the original connection name (rawConnectionName or default) for the cache key, not the connection string value
            (string, string?, string?) cacheKey = (connectionName, taskHubName, azureManagedConnectionString.ClientId);

            this.logger.LogDebug(
                "Getting durability provider for connection '{Connection}', task hub '{TaskHub}', and client ID '{ClientId}'...",
                cacheKey.Item1,
                cacheKey.Item2 ?? "null",
                cacheKey.Item3 ?? "null");

            lock (this.cachedProviders)
            {
                // If a provider has already been created for this connection name, task hub, and client ID, return it.
                if (this.cachedProviders.TryGetValue(cacheKey, out AzureManagedScalabilityProvider? cachedProvider))
                {
                    this.logger.LogDebug(
                        "Returning cached durability provider for connection '{Connection}', task hub '{TaskHub}', and client ID '{ClientId}'",
                        cacheKey.Item1,
                        cacheKey.Item2,
                        cacheKey.Item3 ?? "null");
                    return cachedProvider;
                }

                // Create options from the connection string.
                // For runtime-driven scaling, token credentials are loaded directly from the host.
                AzureManagedOrchestrationServiceOptions options =
                    AzureManagedOrchestrationServiceOptions.FromConnectionString(connectionString);

                // If triggerMetadata is provided (from functions Scale Controller), try to get token credential from it.
                if (triggerMetadata != null && triggerMetadata.Properties != null &&
                    triggerMetadata.Properties.TryGetValue("GetAzureManagedTokenCredential", out object? tokenCredentialFunc))
                {
                    if (tokenCredentialFunc is Func<string, TokenCredential> getTokenCredential)
                    {
                        try
                        {
                            TokenCredential tokenCredential = getTokenCredential(connectionName);

                            if (tokenCredential == null)
                            {
                                this.logger.LogWarning(
                                    "Token credential retrieved from trigger metadata is null for connection '{Connection}'.",
                                    connectionName);
                            }
                            else
                            {
                                // Override the credential from connection string
                                options.TokenCredential = tokenCredential;
                                this.logger.LogInformation("Retrieved token credential from trigger metadata for connection '{Connection}'", connectionName);
                            }
                        }
                        catch (Exception ex)
                        {
                            this.logger.LogWarning(
                                ex,
                                "Failed to get token credential from trigger metadata for connection '{Connection}'",
                                connectionName);
                        }
                    }
                    else
                    {
                        this.logger.LogWarning(
                            "Token credential function pointer in trigger metadata is not of expected type for connection '{Connection}'",
                            connectionName);
                    }
                }
                else
                {
                    this.logger.LogInformation(
                        "No trigger metadata provided or trigger metadata does not contain 'GetAzureManagedTokenCredential', " +
                        "using the token credential built from connection string for connection '{Connection}'.", connectionName);
                }

                // Set task hub name if configured
                if (!string.IsNullOrEmpty(taskHubName))
                {
                    options.TaskHubName = taskHubName;
                }

                // Set concurrency limits from metadata
                if (metadata?.MaxConcurrentOrchestratorFunctions.HasValue == true)
                {
                    options.MaxConcurrentOrchestrationWorkItems = metadata.MaxConcurrentOrchestratorFunctions.Value;
                }

                if (metadata?.MaxConcurrentActivityFunctions.HasValue == true)
                {
                    options.MaxConcurrentActivityWorkItems = metadata.MaxConcurrentActivityFunctions.Value;
                }

                this.logger.LogInformation(
                    "Creating durability provider for connection '{Connection}', task hub '{TaskHub}', and client ID '{ClientId}'...",
                    cacheKey.Item1,
                    cacheKey.Item2,
                    cacheKey.Item3 ?? "null");

                AzureManagedOrchestrationService service = new AzureManagedOrchestrationService(options, this.loggerFactory);
                AzureManagedScalabilityProvider provider = new AzureManagedScalabilityProvider(service, connectionName, this.logger);

                // Extract max concurrent values from trigger metadata (from Scale Controller payload)
                // Default: 10 times the number of processors on the current machine
                provider.MaxConcurrentTaskOrchestrationWorkItems = metadata?.MaxConcurrentOrchestratorFunctions ?? (Environment.ProcessorCount * 10);
                provider.MaxConcurrentTaskActivityWorkItems = metadata?.MaxConcurrentActivityFunctions ?? (Environment.ProcessorCount * 10);

                this.cachedProviders.Add(cacheKey, provider);
                return provider;
              }
          }
      }
  }
