// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.SqlServer;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;
using Azure.Core;

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
            // Validate arguments first
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (nameResolver == null)
            {
                throw new ArgumentNullException(nameof(nameResolver));
            }

            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            // this constructor may be called by dependency injection even if the SQL Server provider is not selected
            // in that case, return immediately, since this provider is not actually used, but can still throw validation errors
            if (options.Value.StorageProvider != null
                && options.Value.StorageProvider.TryGetValue("type", out object value)
                && value is string s
                && !string.Equals(s, this.Name, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            this.options = options.Value;
            this.configuration = configuration;
            this.nameResolver = nameResolver;
            this.loggerFactory = loggerFactory;

            // Resolve default connection name directly from payload keys or fall back
            this.DefaultConnectionName = ResolveConnectionName(options.Value.StorageProvider) ?? "SQLDB_Connection";
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
            // This follows the same pattern as Azure Storage
            var tokenCredential = ExtractTokenCredential(triggerMetadata);

            // Extract connection name from triggerMetadata (similar to how Azure Storage does it)
            // The triggerMetadata contains storageProvider with connectionName or connectionStringName
            string connectionName = this.DefaultConnectionName;
            if (triggerMetadata?.Metadata != null)
            {
                var storageProvider = triggerMetadata.Metadata["storageProvider"];
                if (storageProvider != null)
                {
                    var storageProviderObj = storageProvider.ToObject<System.Collections.Generic.Dictionary<string, object>>();
                    if (storageProviderObj != null)
                    {
                        // Try connectionName first, then connectionStringName (legacy alias)
                        if (storageProviderObj.TryGetValue("connectionName", out object connName) && connName is string connNameStr && !string.IsNullOrWhiteSpace(connNameStr))
                        {
                            connectionName = connNameStr;
                        }
                        else if (storageProviderObj.TryGetValue("connectionStringName", out object connStrName) && connStrName is string connStrNameStr && !string.IsNullOrWhiteSpace(connStrNameStr))
                        {
                            connectionName = connStrNameStr;
                        }
                    }
                }
            }

            var sqlOrchestrationService = this.CreateSqlOrchestrationService(
                connectionName,
                this.options.HubName ?? "default",
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
            string connectionString = null;

            // If TokenCredential is provided (Managed Identity), we need to build connection string from config
            // SQL Server authentication with Managed Identity requires:
            // - Server name (can be in connection string or config: {connectionName}__serverName)
            // - Database name (can be in connection string or config: {connectionName}__databaseName)
            // - Authentication="Active Directory Default" in connection string
            if (tokenCredential != null)
            {
                // For Managed Identity, read server name and database from configuration
                // Pattern: {connectionName}__serverName, {connectionName}__databaseName
                // Or fall back to parsing from connection string if available
                // Note: Server name can also come from the connection string itself
                var serverName = this.configuration[$"{connectionName}__serverName"]
                              ?? this.configuration[$"{connectionName}__server"];
                var databaseName = this.configuration[$"{connectionName}__databaseName"]
                                ?? this.configuration[$"{connectionName}__database"];

                // Try to get base connection string to extract server/database if not explicitly set
                var baseConnectionString = this.configuration.GetConnectionString(connectionName)
                                        ?? this.configuration[connectionName];

                if (!string.IsNullOrEmpty(baseConnectionString))
                {
                    try
                    {
                        var builder = new SqlConnectionStringBuilder(baseConnectionString);
                        // Use explicit config values if provided, otherwise use values from connection string
                        if (string.IsNullOrEmpty(serverName))
                        {
                            serverName = builder.DataSource;
                        }
                        if (string.IsNullOrEmpty(databaseName))
                        {
                            databaseName = builder.InitialCatalog;
                        }

                        // Build connection string with Managed Identity authentication
                        builder.DataSource = serverName;
                        builder.InitialCatalog = databaseName ?? builder.InitialCatalog;
                        builder.Authentication = SqlAuthenticationMethod.ActiveDirectoryDefault;
                        // Remove password/user ID if present (not needed for Managed Identity)
                        builder.Password = null;
                        builder.UserID = null;
                        
                        connectionString = builder.ConnectionString;
                    }
                    catch (ArgumentException)
                    {
                        // If connection string parsing fails, try to construct from config values
                    }
                }

                // If we still don't have connection string, construct from config values
                if (string.IsNullOrEmpty(connectionString))
                {
                    if (string.IsNullOrEmpty(serverName))
                    {
                        throw new InvalidOperationException(
                            $"No SQL server name configuration was found for Managed Identity. Please provide '{connectionName}__serverName' or '{connectionName}__server' app setting, or ensure '{connectionName}' connection string contains server name.");
                    }

                    var connectionStringBuilder = new SqlConnectionStringBuilder
                    {
                        DataSource = serverName,
                        InitialCatalog = databaseName ?? "master",
                        Authentication = SqlAuthenticationMethod.ActiveDirectoryDefault,
                        Encrypt = true,
                    };
                    connectionString = connectionStringBuilder.ConnectionString;
                }
            }
            else
            {
                // No TokenCredential - use connection string from configuration (traditional auth)
                connectionString = this.configuration.GetConnectionString(connectionName)
                                ?? this.configuration[connectionName];

                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException(
                        $"No SQL connection string configuration was found for the app setting or environment variable named '{connectionName}'.");
                }
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

            // Create SQL Server orchestration service settings
            var settings = new SqlOrchestrationServiceSettings(
                connectionString,
                taskHubName,
                schemaName: null) // Schema name can be configured from storageProvider if needed
            {
                // Set concurrency limits if provided
                MaxActiveOrchestrations = this.options.MaxConcurrentOrchestratorFunctions ?? 10,
                MaxConcurrentActivities = this.options.MaxConcurrentActivityFunctions ?? 10,
            };

            // If TokenCredential is provided (from triggerMetadata in Azure), we need to use it instead of DefaultAzureCredential
            // Register a custom SqlAuthenticationProvider that uses our specific TokenCredential
            if (tokenCredential != null)
            {
                // Register custom authentication provider that uses the provided TokenCredential
                // This ensures we use the TokenCredential from Scale Controller, not DefaultAzureCredential
                var customProvider = new CustomTokenCredentialAuthenticationProvider(tokenCredential, logger);
                SqlAuthenticationProvider.SetProvider(
                    SqlAuthenticationMethod.ActiveDirectoryDefault,
                    customProvider);
            }
            // Note: When tokenCredential is null (local development), we use Authentication="Active Directory Default"
            // which will use DefaultAzureCredential. This works for local testing.

            // Create and return the orchestration service
            return new SqlOrchestrationService(settings);
        }

        private static global::Azure.Core.TokenCredential ExtractTokenCredential(TriggerMetadata triggerMetadata)
        {
            if (triggerMetadata?.Properties == null)
            {
                return null;
            }

            // Check if metadata contains an AzureComponentFactory wrapper
            // ScaleController passes it as: metadata.Properties[nameof(AzureComponentFactory)] = new AzureComponentFactoryWrapper(...)
            // This follows the same pattern as Azure Storage
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

        /// <summary>
        /// Custom SqlAuthenticationProvider that uses a specific TokenCredential instead of DefaultAzureCredential.
        /// This allows us to use the TokenCredential passed from triggerMetadata in Azure environments.
        /// </summary>
        private class CustomTokenCredentialAuthenticationProvider : SqlAuthenticationProvider
        {
            private readonly TokenCredential tokenCredential;
            private readonly ILogger logger;
            private const string SqlResource = "https://database.windows.net/.default";

            public CustomTokenCredentialAuthenticationProvider(TokenCredential tokenCredential, ILogger logger)
            {
                this.tokenCredential = tokenCredential ?? throw new ArgumentNullException(nameof(tokenCredential));
                this.logger = logger;
            }

            public override async Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters)
            {
                try
                {
                    // Get token from the provided TokenCredential
                    var tokenRequestContext = new TokenRequestContext(new[] { SqlResource });
                    var accessToken = await this.tokenCredential.GetTokenAsync(tokenRequestContext, CancellationToken.None);
                    
                    return new SqlAuthenticationToken(accessToken.Token, accessToken.ExpiresOn);
                }
                catch (Exception ex)
                {
                    this.logger?.LogError(ex, "Failed to acquire token from TokenCredential for SQL authentication");
                    throw;
                }
            }

            public override bool IsSupported(SqlAuthenticationMethod authenticationMethod)
            {
                // Only support Active Directory Default authentication
                return authenticationMethod == SqlAuthenticationMethod.ActiveDirectoryDefault;
            }
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
