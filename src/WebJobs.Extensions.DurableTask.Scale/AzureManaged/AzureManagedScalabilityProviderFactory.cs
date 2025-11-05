// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using Azure.Core;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.DurableTask.AzureManagedBackend;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

#nullable enable
namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.AzureManaged
{
    public class AzureManagedScalabilityProviderFactory : IScalabilityProviderFactory
    {
        private const string LoggerName = "Host.Triggers.DurableTask.AzureManaged";
        internal const string ProviderName = "AzureManaged";
        private const string DefaultConnectionNameConstant = "DURABLE_TASK_SCHEDULER_CONNECTION_STRING";

        private readonly Dictionary<(string, string?, string?), AzureManagedScalabilityProvider> cachedProviders = new Dictionary<(string, string?, string?), AzureManagedScalabilityProvider>();
        private readonly DurableTaskScaleOptions options;
        private readonly IConfiguration configuration;
        private readonly INameResolver nameResolver;
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger logger;

        public AzureManagedScalabilityProviderFactory(
            IOptions<DurableTaskScaleOptions> options,
            IConfiguration configuration,
            INameResolver nameResolver,
            ILoggerFactory loggerFactory)
        {
            this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.nameResolver = nameResolver ?? throw new ArgumentNullException(nameof(nameResolver));
            this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            this.logger = this.loggerFactory.CreateLogger(LoggerName);

            this.DefaultConnectionName = ResolveConnectionName(this.options.StorageProvider) ?? DefaultConnectionNameConstant;
        }

        public virtual string Name => ProviderName;

        public string DefaultConnectionName { get; }

        public virtual ScalabilityProvider GetDurabilityProvider()
        {
            return this.GetDurabilityProvider(null);
        }

        public ScalabilityProvider GetDurabilityProvider(TriggerMetadata triggerMetadata)
        {
            // Check if trigger metadata specifies a different connection name, otherwise use default from constructor
            string connectionName = ExtractConnectionName(triggerMetadata) ?? this.DefaultConnectionName;

            // Resolve connection name first (handles %% wrapping)
            string resolvedConnectionName = this.nameResolver.Resolve(connectionName);
            
            // Try to get connection string from configuration (app settings)
            string connectionString = this.configuration.GetConnectionString(resolvedConnectionName)
                                   ?? this.configuration[resolvedConnectionName];
            
            // Fallback to environment variable (matching old implementation behavior)
            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = Environment.GetEnvironmentVariable(resolvedConnectionName);
            }

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    $"No connection string configuration was found for the app setting or environment variable named '{resolvedConnectionName}'.");
            }

            AzureManagedConnectionString azureManagedConnectionString = new AzureManagedConnectionString(connectionString);
            
            // Get the pre-parsed metadata from triggerMetadata.Properties (parsed by DurableTaskTriggersScaleProvider)
            DurableTaskMetadata parsedMetadata = ExtractParsedMetadata(triggerMetadata);
            
            // Extract task hub name from parsed metadata first, fallback to DI options, then connection string
            string taskHubName = parsedMetadata?.TaskHubName
                ?? this.options.HubName
                ?? azureManagedConnectionString.TaskHubName;

            // Include client ID in cache key to handle managed identity changes
            (string, string?, string?) cacheKey = (connectionName, taskHubName, azureManagedConnectionString.ClientId);

            this.logger.LogDebug(
                "Getting durability provider for connection '{Connection}', task hub '{TaskHub}', and client ID '{ClientId}'...",
                cacheKey.Item1, cacheKey.Item2, cacheKey.Item3 ?? "null");

            lock (this.cachedProviders)
            {
                // If a provider has already been created for this connection name, task hub, and client ID, return it.
                if (this.cachedProviders.TryGetValue(cacheKey, out AzureManagedScalabilityProvider? cachedProvider))
                {
                    this.logger.LogDebug(
                        "Returning cached durability provider for connection '{Connection}', task hub '{TaskHub}', and client ID '{ClientId}'",
                        cacheKey.Item1, cacheKey.Item2, cacheKey.Item3 ?? "null");
                    return cachedProvider;
                }

                // Create options from the connection string
                AzureManagedOrchestrationServiceOptions options =
                    AzureManagedOrchestrationServiceOptions.FromConnectionString(connectionString);

                // If triggerMetadata is provided, try to get token credential from it
                if (triggerMetadata != null && triggerMetadata.Properties != null && 
                    triggerMetadata.Properties.TryGetValue("GetAzureManagedTokenCredential", out object tokenCredentialFunc))
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

                // Set concurrency limits
                if (this.options.MaxConcurrentOrchestratorFunctions.HasValue)
                {
                    options.MaxConcurrentOrchestrationWorkItems = this.options.MaxConcurrentOrchestratorFunctions.Value;
                }

                if (this.options.MaxConcurrentActivityFunctions.HasValue)
                {
                    options.MaxConcurrentActivityWorkItems = this.options.MaxConcurrentActivityFunctions.Value;
                }

                this.logger.LogInformation(
                    "Creating durability provider for connection '{Connection}', task hub '{TaskHub}', and client ID '{ClientId}'...",
                    cacheKey.Item1, cacheKey.Item2, cacheKey.Item3 ?? "null");

                AzureManagedOrchestrationService service = new AzureManagedOrchestrationService(options, this.loggerFactory);
                AzureManagedScalabilityProvider provider = new AzureManagedScalabilityProvider(service, connectionName, this.logger);

                // Extract max concurrent values from parsed metadata first, fallback to DI options
                provider.MaxConcurrentTaskOrchestrationWorkItems = parsedMetadata?.MaxConcurrentOrchestratorFunctions 
                    ?? this.options.MaxConcurrentOrchestratorFunctions 
                    ?? 10;
                provider.MaxConcurrentTaskActivityWorkItems = parsedMetadata?.MaxConcurrentActivityFunctions 
                    ?? this.options.MaxConcurrentActivityFunctions 
                    ?? 10;

                this.cachedProviders.Add(cacheKey, provider);
                return provider;
            }
        }

        private static string ExtractConnectionName(TriggerMetadata triggerMetadata)
        {
            if (triggerMetadata?.Metadata == null)
            {
                return null;
            }

            var storageProvider = triggerMetadata.Metadata["storageProvider"];
            if (storageProvider != null)
            {
                var storageProviderObj = storageProvider.ToObject<Dictionary<string, object>>();
                if (storageProviderObj != null)
                {
                    // Try connectionName first, then connectionStringName (legacy alias)
                    if (storageProviderObj.TryGetValue("connectionName", out object connName) && connName is string connNameStr && !string.IsNullOrWhiteSpace(connNameStr))
                    {
                        return connNameStr;
                    }

                    if (storageProviderObj.TryGetValue("connectionStringName", out object connStrName) && connStrName is string connStrNameStr && !string.IsNullOrWhiteSpace(connStrNameStr))
                    {
                        return connStrNameStr;
                    }
                }
            }

            return null;
        }

        private static string ResolveConnectionName(IDictionary<string, object> storageProvider)
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

