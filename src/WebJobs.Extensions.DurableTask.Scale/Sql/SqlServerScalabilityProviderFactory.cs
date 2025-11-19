// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable

using System;
using DurableTask.SqlServer;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Sql
{
    /// <summary>
    /// Factory for creating SQL Server scalability providers.
    /// </summary>
    public class SqlServerScalabilityProviderFactory : IScalabilityProviderFactory
    {
        private const string LoggerName = "Triggers.DurableTask.SqlServer";
        internal const string ProviderName = "mssql";

        private readonly IConfiguration configuration;
        private readonly INameResolver nameResolver;
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerScalabilityProviderFactory"/> class.
        /// </summary>
        /// <param name="configuration">The configuration for reading connection strings.</param>
        /// <param name="nameResolver">The name resolver for connection strings.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
        public SqlServerScalabilityProviderFactory(
            IConfiguration configuration,
            INameResolver nameResolver,
            ILoggerFactory loggerFactory)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.nameResolver = nameResolver ?? throw new ArgumentNullException(nameof(nameResolver));
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
        /// Gets the default scalability provider for this factory.
        /// This method should never be called for SQL provider as metadata is always required.
        /// </summary>
        /// <returns> A default <see cref="SqlServerScalabilityProvider"/> instance.</returns>
        /// <exception cref="NotImplementedException">Always throws as this method should not be called.</exception>
        public virtual ScalabilityProvider GetScalabilityProvider()
        {
            throw new NotImplementedException("SQL provider requires metadata and should not use parameterless GetScalabilityProvider()");
        }

        /// <summary>
        /// Creates a scalability provider using pre-deserialized metadata.
        /// </summary>
        /// <param name="metadata">The pre-deserialized Durable Task metadata.</param>
        /// <param name="triggerMetadata">Trigger metadata (for future extensions, e.g., token credentials).</param>
        /// <returns>A configured SQL Server scalability provider.</returns>
        public ScalabilityProvider GetScalabilityProvider(DurableTaskMetadata metadata, TriggerMetadata triggerMetadata)
        {
            // Validate SQL Server specific metadata if present
            if (metadata != null)
            {
                this.ValidateSqlServerMetadata(metadata, this.logger);
            }

            // Resolve connection name: prioritize metadata, fallback to default
            string? rawConnectionName = TriggerMetadataExtensions.ResolveConnectionName(metadata?.StorageProvider);
            string connectionName = rawConnectionName != null
                ? this.nameResolver.Resolve(rawConnectionName)
                : this.DefaultConnectionName;

            // Extract task hub name from trigger metadata (from Scale Controller payload)
            string taskHubName = metadata?.TaskHubName ?? "default";

            var sqlOrchestrationService = this.CreateSqlOrchestrationService(
                connectionName,
                taskHubName,
                this.logger,
                metadata);

            var provider = new SqlServerScalabilityProvider(
                sqlOrchestrationService,
                connectionName,
                this.logger);

            return provider;
        }

        private SqlOrchestrationService CreateSqlOrchestrationService(
            string connectionName,
            string taskHubName,
            ILogger logger,
            DurableTaskMetadata? metadata = null)
        {
            // Resolve connection name first (handles %% wrapping)
            string resolvedValue = this.nameResolver.Resolve(connectionName);

            // nameResolver.Resolve() may return either:
            // 1. The connection name (if it's an app setting name like "MyConnection")
            // 2. The connection string value itself (if it's already resolved or is an environment variable)
            // Check if resolvedValue looks like a connection string (contains "=" which is typical for connection strings)
            // If it does, use it directly; otherwise, treat it as a connection name and look it up
            string? connectionString = null;

            if (!string.IsNullOrEmpty(resolvedValue) && resolvedValue.Contains("="))
            {
                // resolvedValue is already a connection string
                connectionString = resolvedValue;
            }
            else
            {
                // resolvedValue is a connection name, look it up
                connectionString =
                    this.configuration.GetConnectionString(resolvedValue) ??
                    this.configuration[resolvedValue] ??
                    Environment.GetEnvironmentVariable(resolvedValue);
            }

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    $"No SQL connection string configuration was found for the app setting or environment variable named '{resolvedValue}'.");
            }

            // Create SQL Server orchestration service settings - following durabletask-mssql pattern
            // Connection string should include authentication method (e.g., Authentication=Active Directory Default)
            var settings = new SqlOrchestrationServiceSettings(
                connectionString,
                taskHubName)
            {
                // Set concurrency limits from trigger metadata (from Scale Controller payload)
                // Default: 10 times the number of processors on the current machine
                MaxActiveOrchestrations = metadata?.MaxConcurrentOrchestratorFunctions ?? (Environment.ProcessorCount * 10),
                MaxConcurrentActivities = metadata?.MaxConcurrentActivityFunctions ?? (Environment.ProcessorCount * 10),
            };

            // Note: When connection string includes "Authentication=Active Directory Default" or
            // "Authentication=Active Directory Managed Identity", SQL Server will automatically use
            // the appropriate Azure identity (managed identity in Azure, or DefaultAzureCredential locally).
            // So we don't need to exctract token crednetial here from sitemetada.

            return new SqlOrchestrationService(settings);
        }

        /// <summary>
        /// Validates SQL Server specific metadata.
        /// </summary>
        private void ValidateSqlServerMetadata(DurableTaskMetadata metadata, ILogger logger)
        {
            // Validate hub name (SQL Server has less strict requirements than Azure Storage)
            if (string.IsNullOrWhiteSpace(metadata.TaskHubName))
            {
                // Hub name defaults to "default" for SQL Server, so this is acceptable
                return;
            }

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
