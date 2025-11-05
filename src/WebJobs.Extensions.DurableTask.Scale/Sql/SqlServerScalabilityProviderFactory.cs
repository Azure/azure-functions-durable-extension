// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using DurableTask.SqlServer;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

        public virtual ScalabilityProvider GetScalabilityProvider()
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
                    logger);

                this.defaultSqlProvider = new SqlServerScalabilityProvider(
                    sqlOrchestrationService,
                    this.DefaultConnectionName,
                    logger);
            }

            return this.defaultSqlProvider;
        }

        public ScalabilityProvider GetScalabilityProvider(TriggerMetadata triggerMetadata)
        {
            ILogger logger = this.loggerFactory.CreateLogger(LoggerName);

            // Validate SQL Server specific options
            this.ValidateSqlServerOptions(logger);

            // Extract task hub name from trigger options (already built from metadata)
            string taskHubName = this.options.HubName ?? "default";

            var sqlOrchestrationService = this.CreateSqlOrchestrationService(
                this.DefaultConnectionName,
                taskHubName,
                logger);

            var provider = new SqlServerScalabilityProvider(
                sqlOrchestrationService,
                this.DefaultConnectionName,
                logger);

            return provider;
        }

        private SqlOrchestrationService CreateSqlOrchestrationService(
            string connectionName,
            string taskHubName,
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

            // Create SQL Server orchestration service settings - following durabletask-mssql pattern
            // Connection string should include authentication method (e.g., Authentication=Active Directory Default)
            var settings = new SqlOrchestrationServiceSettings(
                connectionString,
                taskHubName)
            {
                // Set concurrency limits if provided
                MaxActiveOrchestrations = this.options.MaxConcurrentOrchestratorFunctions ?? 10,
                MaxConcurrentActivities = this.options.MaxConcurrentActivityFunctions ?? 10,
            };

            // Note: When connection string includes "Authentication=Active Directory Default" or
            // "Authentication=Active Directory Managed Identity", SQL Server will automatically use
            // the appropriate Azure identity (managed identity in Azure, or DefaultAzureCredential locally).
            // So we don't need to exctract token crednetial here from sitemetada.

            return new SqlOrchestrationService(settings);
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
    }
}
