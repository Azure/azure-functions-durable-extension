// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable

using System;
using DurableTask.SqlServer;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Sql
{
    /// <summary>
    /// Factory for creating SQL Server scalability providers.
    /// </summary>
    public class SqlServerScalabilityProviderFactory : IScalabilityProviderFactory
    {
        private const string LoggerName = "Triggers.DurableTask.SqlServer";
        private const string ProviderName = "mssql";

        private readonly IConfiguration configuration;
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerScalabilityProviderFactory"/> class.
        /// </summary>
        /// <param name="configuration">The configuration for reading connection strings.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
        public SqlServerScalabilityProviderFactory(
            IConfiguration configuration,
            ILoggerFactory loggerFactory)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            this.logger = this.loggerFactory.CreateLogger(LoggerName);
            this.DefaultConnectionName = "SQLDB_Connection";
        }

        /// <summary>
        /// Gets the name of durabletask-mssql backend provider.
        /// </summary>
        public virtual string Name => ProviderName;

        /// <summary>
        /// Gets the default connection name of durabletask-mssql backend provider.
        /// </summary>
        public string DefaultConnectionName { get; }

        /// <summary>
        /// Creates a scalability provider using pre-deserialized metadata.
        /// </summary>
        /// <param name="metadata">The pre-deserialized Durable Task metadata.</param>
        /// <param name="triggerMetadata">Trigger metadata (for future extensions, e.g., token credentials).</param>
        /// <returns>A configured SQL Server scalability provider.</returns>
        public ScalabilityProvider GetScalabilityProvider(DurableTaskMetadata metadata, TriggerMetadata? triggerMetadata)
        {
            // Validate SQL Server specific metadata if present
            this.ValidateSqlServerMetadata(metadata);

            // Get connection name from metadata, fallback to default
            // Note: Scale Controller already resolves %xxx% wrapping before calling the extension,
            // so we just need to get the raw value here
            string? rawConnectionName = TriggerMetadataExtensions.ResolveConnectionName(metadata?.StorageProvider);
            string connectionName = rawConnectionName ?? this.DefaultConnectionName;

            // Extract task hub name from trigger metadata (from Scale Controller payload)
            string taskHubName = metadata?.TaskHubName ?? "default";

            var sqlOrchestrationService = this.CreateSqlOrchestrationService(
                connectionName,
                taskHubName,
                metadata);

            var provider = new SqlServerScalabilityProvider(
                sqlOrchestrationService,
                connectionName);

            return provider;
        }

        /// <summary>
        /// Creates a SqlOrchestrationService from a connection name.
        /// </summary>
        private SqlOrchestrationService CreateSqlOrchestrationService(
            string connectionName,
            string taskHubName,
            DurableTaskMetadata? metadata = null)
        {
            // Look up connection string from configuration
            this.logger.LogInformation("Using connection name {ConnectionName}", connectionName);
            string? connectionString =
                this.configuration.GetConnectionString(connectionName) ??
                this.configuration[connectionName] ??
                Environment.GetEnvironmentVariable(connectionName);

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    $"No SQL connection string found for '{connectionName}'.");
            }

            // Create SQL Server orchestration service settings - following durabletask-mssql pattern
            // Connection string should include authentication method (e.g., Authentication=Active Directory Default)
            var settings = new SqlOrchestrationServiceSettings(
                connectionString,
                taskHubName)
            {
                // Set concurrency limits from trigger metadata (from Scale Controller payload)
                // Default: 10
                MaxActiveOrchestrations = metadata?.MaxConcurrentOrchestratorFunctions ?? 10,
                MaxConcurrentActivities = metadata?.MaxConcurrentActivityFunctions ?? 10,
            };

            // Note: When connection string includes "Authentication=Active Directory Default" or
            // "Authentication=Active Directory Managed Identity", SQL Server will automatically use
            // the appropriate Azure identity (managed identity in Azure, or DefaultAzureCredential locally).
            // So we don't need to extract token credential here from site metadata.
            return new SqlOrchestrationService(settings);
        }

        /// <summary>
        /// Validates SQL Server specific metadata.
        /// </summary>
        private void ValidateSqlServerMetadata(DurableTaskMetadata metadata)
        {
            // Validate max concurrent orchestrator functions
            if (metadata.MaxConcurrentOrchestratorFunctions.HasValue && metadata.MaxConcurrentOrchestratorFunctions.Value <= 0)
            {
                throw new InvalidOperationException($"{nameof(metadata.MaxConcurrentOrchestratorFunctions)} must be a positive integer.");
            }

            // Validate max concurrent activity functions
            if (metadata.MaxConcurrentActivityFunctions.HasValue && metadata.MaxConcurrentActivityFunctions.Value <= 0)
            {
                throw new InvalidOperationException($"{nameof(metadata.MaxConcurrentActivityFunctions)} must be a positive integer.");
            }
        }
    }
}
