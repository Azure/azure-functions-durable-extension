// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using Azure.Core;
using DurableTask.SqlServer;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Sql
{
    /// <summary>
    /// Factory for creating SQL Server scalability providers.
    /// </summary>
    public class SqlServerScalabilityProviderFactory : IScalabilityProviderFactory
    {
        private const string LoggerName = "Host.Triggers.DurableTask.SqlServer";
        internal const string ProviderName = "mssql";

        private readonly DurableTaskScaleOptions options;
        private readonly IConfiguration configuration;
        private readonly INameResolver nameResolver;
        private readonly ILoggerFactory loggerFactory;
        private SqlServerScalabilityProvider defaultSqlProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerScalabilityProviderFactory"/> class.
        /// </summary>
        /// <param name="options">The durable task scale options.</param>
        /// <param name="configuration">The configuration for reading connection strings.</param>
        /// <param name="nameResolver">The name resolver for connection strings.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
        public SqlServerScalabilityProviderFactory(
            IOptions<DurableTaskScaleOptions> options,
            IConfiguration configuration,
            INameResolver nameResolver,
            ILoggerFactory loggerFactory)
        {
            this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.nameResolver = nameResolver ?? throw new ArgumentNullException(nameof(nameResolver));
            this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

            this.DefaultConnectionName = ResolveConnectionName(this.options.StorageProvider) ?? "SQLDB_Connection";
        }

        public virtual string Name => ProviderName;

        public string DefaultConnectionName { get; }

        public virtual ScalabilityProvider GetDurabilityProvider()
        {
            if (this.defaultSqlProvider == null)
            {
                ILogger logger = this.loggerFactory.CreateLogger(LoggerName);

                // Validate SQL Server specific options
                this.ValidateSqlServerOptions(logger);

                // Create SqlOrchestrationService from connection string
                // No TokenCredential for default provider (uses connection string auth)
                var sqlOrchestrationService = this.CreateSqlOrchestrationService(
                    this.DefaultConnectionName,
                    this.options.HubName ?? "default",
                    tokenCredential: null,
                    logger);

                this.defaultSqlProvider = new SqlServerScalabilityProvider(
                    sqlOrchestrationService,
                    this.DefaultConnectionName,
                    logger);

                // Set the max concurrent values from options (if needed by SQL Server)
                // Note: SQL Server uses MaxActiveOrchestrations and MaxConcurrentActivities in settings
                // These are set when creating SqlOrchestrationServiceSettings
            }

            return this.defaultSqlProvider;
        }

        public ScalabilityProvider GetDurabilityProvider(TriggerMetadata triggerMetadata)
        {
            ILogger logger = this.loggerFactory.CreateLogger(LoggerName);

            // Validate SQL Server specific options
            this.ValidateSqlServerOptions(logger);

            // Extract TokenCredential from triggerMetadata if present (for Managed Identity)
            var tokenCredential = ExtractTokenCredential(triggerMetadata);

            // Get the pre-parsed metadata from triggerMetadata.Properties (parsed by DurableTaskTriggersScaleProvider)
            DurableTaskMetadata parsedMetadata = ExtractParsedMetadata(triggerMetadata);

            // Check if trigger metadata specifies a different connection name, otherwise use default from constructor
            string connectionName = ExtractConnectionName(triggerMetadata) ?? this.DefaultConnectionName;

            // Extract task hub name from parsed metadata first, fallback to DI options
            string taskHubName = parsedMetadata?.TaskHubName 
                ?? this.options.HubName 
                ?? "default";

            var sqlOrchestrationService = this.CreateSqlOrchestrationService(
                connectionName,
                taskHubName,
                tokenCredential,
                logger);

            var provider = new SqlServerScalabilityProvider(
                sqlOrchestrationService,
                connectionName,
                logger);

            return provider;
        }

        private SqlOrchestrationService CreateSqlOrchestrationService(
            string connectionName,
            string taskHubName,
            global::Azure.Core.TokenCredential tokenCredential,
            ILogger logger)
        {
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
                    $"No SQL connection string configuration was found for the app setting or environment variable named '{resolvedConnectionName}'.");
            }

            // Validate the connection string
            try
            {
                new SqlConnectionStringBuilder(connectionString);
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException("The provided connection string is invalid.", e);
            }

            // Create SQL Server orchestration service settings - following durabletask-mssql pattern
            // Connection string should include authentication method (e.g., Authentication=Active Directory Default)
            var settings = new SqlOrchestrationServiceSettings(
                connectionString,
                taskHubName,
                schemaName: null) // Schema name can be configured from storageProvider if needed
            {
                // Set concurrency limits if provided
                MaxActiveOrchestrations = this.options.MaxConcurrentOrchestratorFunctions ?? 10,
                MaxConcurrentActivities = this.options.MaxConcurrentActivityFunctions ?? 10,
            };

            // Note: When connection string includes "Authentication=Active Directory Default" or 
            // "Authentication=Active Directory Managed Identity", SQL Server will automatically use
            // the appropriate Azure identity (managed identity in Azure, or DefaultAzureCredential locally).
            // The tokenCredential from Scale Controller is primarily for Azure Storage; SQL Server 
            // manages its own token acquisition through the connection string's Authentication setting.

            // Create and return the orchestration service
            return new SqlOrchestrationService(settings);
        }

        // Note: ExtractTokenCredential is kept for potential future use, but SQL Server handles 
        // its own authentication through the connection string (Authentication=Active Directory Default)
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

        /// <summary>
        /// Validates SQL Server specific options.
        /// </summary>
        private void ValidateSqlServerOptions(ILogger logger)
        {
            // Validate hub name (SQL Server has less strict requirements than Azure Storage)
            if (string.IsNullOrWhiteSpace(this.options.HubName))
            {
                // Hub name defaults to "default" for SQL Server, so this is acceptable
                return;
            }

            // Validate max concurrent orchestrator functions
            if (this.options.MaxConcurrentOrchestratorFunctions.HasValue && this.options.MaxConcurrentOrchestratorFunctions.Value <= 0)
            {
                throw new InvalidOperationException($"{nameof(this.options.MaxConcurrentOrchestratorFunctions)} must be a positive integer.");
            }

            // Validate max concurrent activity functions
            if (this.options.MaxConcurrentActivityFunctions.HasValue && this.options.MaxConcurrentActivityFunctions.Value <= 0)
            {
                throw new InvalidOperationException($"{nameof(this.options.MaxConcurrentActivityFunctions)} must be a positive integer.");
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
