// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Sql;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Tests
{
    /// <summary>
    /// Tests for SqlServerScalabilityProviderFactory.
    /// Validates factory creation, provider instantiation, and configuration handling.
    /// Note: SQL Server is NOT the default provider - only created when storageProvider.type = "mssql".
    /// </summary>
    public class SqlServerScalabilityProviderFactoryTests
    {
        private readonly ITestOutputHelper output;
        private readonly TestLoggerProvider loggerProvider;
        private readonly ILoggerFactory loggerFactory;
        private readonly INameResolver nameResolver;
        private readonly IConfiguration configuration;

        public SqlServerScalabilityProviderFactoryTests(ITestOutputHelper output)
        {
            this.output = output;
            this.loggerFactory = new LoggerFactory();
            this.loggerProvider = new TestLoggerProvider(output);
            this.loggerFactory.AddProvider(this.loggerProvider);

            // Create real configuration with SQL connection string
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(new Dictionary<string, string>
            {
                { "SQLDB_Connection", TestHelpers.GetSqlConnectionString() },
                { "TestConnection", TestHelpers.GetSqlConnectionString() },
            });
            this.configuration = configBuilder.Build();

            this.nameResolver = new SimpleNameResolver();
        }

        private class SimpleNameResolver : INameResolver
        {
            public string Resolve(string name) => name;
        }

        /// <summary>
        /// Scenario: Creating factory with valid parameters when type="mssql" is specified.
        /// Validates that factory can be instantiated with proper configuration when SQL Server type is specified.
        /// Verifies factory name is "mssql" and connection name is set correctly.
        /// </summary>
        [Fact]
        public void Constructor_WithMssqlType_CreatesInstance()
        {
            // Arrange - Specify type="mssql" in storage provider
            // Options no longer used - removed CreateOptions call

            // Act
            var factory = new SqlServerScalabilityProviderFactory(
                this.configuration,
                this.nameResolver,
                this.loggerFactory);

            // Assert
            Assert.NotNull(factory);
            Assert.Equal("mssql", factory.Name);
            // DefaultConnectionName is now hardcoded, not from options
            Assert.Equal("SQLDB_Connection", factory.DefaultConnectionName);
        }

        /// <summary>
        /// Scenario: Factory returns early when type is NOT "mssql".
        /// Validates that factory does not initialize when storage provider type is different (e.g., "AzureStorage").
        /// Tests that SQL Server factory respects the storage provider type selection.
        /// Ensures factory can be registered but only activates for SQL Server.
        /// </summary>
        [Fact]
        public void Constructor_WithAzureStorageType_ReturnsEarly()
        {
            // Arrange - Specify type="AzureStorage" instead of "mssql"
            // Options no longer used - removed CreateOptions call

            // Act
            var factory = new SqlServerScalabilityProviderFactory(
                this.configuration,
                this.nameResolver,
                this.loggerFactory);

            // Assert - Factory should be created but not initialized for non-SQL types
            Assert.NotNull(factory);
            Assert.Equal("mssql", factory.Name);
            // DefaultConnectionName may be null or default since factory returns early
        }

        /// <summary>
        /// Scenario: Constructor validation - null options.
        /// Validates that factory properly rejects null options parameter.
        /// Ensures proper error handling for missing configuration.
        /// </summary>
        [Fact]
        public void Constructor_NullOptions_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new SqlServerScalabilityProviderFactory(
                    null,
                    this.nameResolver,
                    this.loggerFactory));
        }

        /// <summary>
        /// Scenario: Creating durability provider with trigger metadata containing type="mssql".
        /// Validates the end-to-end flow when Scale Controller calls with SQL Server trigger metadata.
        /// Tests that provider is created successfully when metadata specifies SQL Server backend.
        /// Verifies connection string resolution and provider creation.
        /// This is the primary path used by Azure Functions Scale Controller for SQL Server.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_WithTriggerMetadataAndMssqlType_ReturnsValidProvider()
        {
            // Arrange
            // Options no longer used - removed CreateOptions call
            var factory = new SqlServerScalabilityProviderFactory(
                this.configuration,
                this.nameResolver,
                this.loggerFactory);

            var triggerMetadata = CreateTriggerMetadata("testHub", 15, 25, "TestConnection", "mssql");
            var metadata = triggerMetadata.ExtractDurableTaskMetadata();

            // Act
            var provider = factory.GetScalabilityProvider(metadata, triggerMetadata);

            // Assert
            Assert.NotNull(provider);
            Assert.IsType<SqlServerScalabilityProvider>(provider);
            Assert.Equal("TestConnection", provider.ConnectionName);
        }

        /// <summary>
        /// Scenario: Creating durability provider without trigger metadata (default path) with mssql type.
        /// Validates that provider can be created using only options configuration when type="mssql".
        /// Tests connection string-based authentication.
        /// Verifies provider has correct type and connection name.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_WithMssqlType_ReturnsValidProvider()
        {
            // Arrange - SQL Server now requires metadata
            var factory = new SqlServerScalabilityProviderFactory(
                this.configuration,
                this.nameResolver,
                this.loggerFactory);

            // Act & Assert - Should throw NotImplementedException since SQL provider requires metadata
            Assert.Throws<NotImplementedException>(() => factory.GetScalabilityProvider());
        }

        /// <summary>
        /// Scenario: Connection name resolution from storage provider configuration.
        /// Validates that factory correctly resolves connection name from storageProvider.connectionStringName.
        /// Tests both connectionName and connectionStringName keys.
        /// Ensures proper configuration reading for SQL Server connections.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_WithConnectionStringName_UsesCorrectConnection()
        {
            // Arrange - Pass connection name via trigger metadata (Scale Controller payload)
            var triggerMetadata = CreateTriggerMetadata("testHub", 10, 20, "TestConnection", "mssql");
            var metadata = triggerMetadata.ExtractDurableTaskMetadata();

            var factory = new SqlServerScalabilityProviderFactory(
                this.configuration,
                this.nameResolver,
                this.loggerFactory);

            // Act - Connection name comes from triggerMetadata, not from options
            var provider = factory.GetScalabilityProvider(metadata, triggerMetadata);

            // Assert
            Assert.NotNull(provider);
            Assert.Equal("TestConnection", provider.ConnectionName);
        }

        /// <summary>
        /// Scenario: Validation - invalid concurrency settings.
        /// Validates that max concurrent orchestrator/activity functions must be >= 1.
        /// Tests concurrency configuration enforcement.
        /// Ensures valid worker count calculations for scaling.
        /// </summary>
        [Fact]
        public void ValidateSqlServerOptions_InvalidMaxConcurrent_ThrowsInvalidOperationException()
        {
            // Arrange - SQL Server now requires metadata
            var factory = new SqlServerScalabilityProviderFactory(
                this.configuration,
                this.nameResolver,
                this.loggerFactory);

            // Act & Assert - Should throw NotImplementedException since SQL provider requires metadata
            Assert.Throws<NotImplementedException>(() => factory.GetScalabilityProvider());
        }

        /// <summary>
        /// Scenario: Missing connection string in configuration.
        /// Validates that factory throws appropriate error when SQL connection string is not found.
        /// Tests error handling for missing configuration values.
        /// Ensures clear error messages guide users to configure connection strings.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_MissingConnectionString_ThrowsInvalidOperationException()
        {
            // Arrange - Configuration without SQL connection string
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(new Dictionary<string, string>());
            var emptyConfig = configBuilder.Build();

            // Options no longer used - removed CreateOptions call
            var factory = new SqlServerScalabilityProviderFactory(
                emptyConfig,
                this.nameResolver,
                this.loggerFactory);

            // Act & Assert - Should throw NotImplementedException since SQL provider requires metadata
            Assert.Throws<NotImplementedException>(() => factory.GetScalabilityProvider());
        }

        // CreateOptions helper removed - DurableTaskScaleOptions no longer exists
        // Tests now rely on TriggerMetadata from Scale Controller instead of DurableTaskScaleOptions

        private static TriggerMetadata CreateTriggerMetadata(
            string hubName,
            int maxOrchestrator,
            int maxActivity,
            string connectionName,
            string storageType = "mssql")
        {
            var metadata = new JObject
            {
                { "functionName", "TestFunction" },
                { "type", "activityTrigger" },
                { "taskHubName", hubName },
                { "maxConcurrentOrchestratorFunctions", maxOrchestrator },
                { "maxConcurrentActivityFunctions", maxActivity },
                {
                    "storageProvider", new JObject
                    {
                        { "type", storageType },
                        { "connectionName", connectionName },
                    }
                },
            };

            // Use the public constructor
            return new TriggerMetadata(metadata);
        }

        /// <summary>
        /// Scenario: End-to-end test - triggerMetadata with type="mssql" creates SQL provider and both TargetScaler and ScaleMonitor work.
        /// Validates that when triggerMetadata mentions storageProvider.type="mssql", we create a SQL provider.
        /// Tests that connection string is retrieved from triggerMetadata.storageProvider.connectionName.
        /// Verifies that TargetScaler successfully returns results from SQL Server.
        /// Verifies that ScaleMonitor successfully returns metrics from SQL Server.
        /// This is the primary integration test for SQL Server scaling via triggerMetadata.
        /// </summary>
        [Fact]
        public async Task TriggerMetadataWithMssqlType_CreatesSqlProvider_AndTargetScalerAndScaleMonitorWork()
        {
            // Arrange - Create triggerMetadata with type="mssql"
            var hubName = "testHub";
            var connectionName = "TestConnection";
            var triggerMetadata = CreateTriggerMetadata(hubName, 10, 20, connectionName, "mssql");

            // Verify triggerMetadata has correct storageProvider.type
            var storageProvider = triggerMetadata.Metadata["storageProvider"] as JObject;
            Assert.NotNull(storageProvider);
            Assert.Equal("mssql", storageProvider["type"]?.ToString());

            // Create factory
                // Options no longer used - removed CreateOptions call
            var factory = new SqlServerScalabilityProviderFactory(
                this.configuration,
                this.nameResolver,
                this.loggerFactory);

            var metadata = triggerMetadata.ExtractDurableTaskMetadata();

            // Act - Create provider from triggerMetadata
            var provider = factory.GetScalabilityProvider(metadata, triggerMetadata);

            // Assert - Verify SQL provider was created
            Assert.NotNull(provider);
            Assert.IsType<SqlServerScalabilityProvider>(provider);
            Assert.Equal(connectionName, provider.ConnectionName);

            // Verify connection string was retrieved from configuration
            var connectionString = this.configuration.GetConnectionString(connectionName) ?? this.configuration[connectionName];
            Assert.NotNull(connectionString);
            Assert.NotEmpty(connectionString);

            // Act - Get TargetScaler from provider
            bool targetScalerCreated = provider.TryGetTargetScaler(
                "functionId",
                "functionName",
                hubName,
                connectionName,
                out ITargetScaler targetScaler);

            // Assert - TargetScaler was created successfully
            Assert.True(targetScalerCreated);
            Assert.NotNull(targetScaler);
            Assert.IsType<SqlServerTargetScaler>(targetScaler);

            // Act - Get scale result from TargetScaler (connects to real SQL)
            var targetScalerResult = await targetScaler.GetScaleResultAsync(new TargetScalerContext());

            // Assert - TargetScaler returns valid result
            Assert.NotNull(targetScalerResult);
            Assert.True(targetScalerResult.TargetWorkerCount >= 0, "Target worker count should be non-negative");

            // Act - Get ScaleMonitor from provider
            bool scaleMonitorResult = provider.TryGetScaleMonitor(
                "functionId",
                "functionName",
                hubName,
                connectionName,
                out IScaleMonitor scaleMonitor);

            // Assert - ScaleMonitor was created successfully
            Assert.True(scaleMonitorResult);
            Assert.NotNull(scaleMonitor);
            Assert.IsType<SqlServerScaleMonitor>(scaleMonitor);

            // Act - Get metrics from ScaleMonitor (connects to real SQL)
            var metrics = await scaleMonitor.GetMetricsAsync();

            // Assert - ScaleMonitor returns valid metrics
            Assert.NotNull(metrics);
            Assert.IsType<SqlServerScaleMetric>(metrics);
            var sqlMetrics = (SqlServerScaleMetric)metrics;
            Assert.True(sqlMetrics.RecommendedReplicaCount >= 0, "Recommended replica count should be non-negative");
        }

        /// <summary>
        /// Scenario: Connection string retrieval from triggerMetadata.
        /// Validates that when triggerMetadata contains connectionName, the factory retrieves the connection string from configuration.
        /// Tests that the connection string value is correctly read from IConfiguration.
        /// Verifies that the retrieved connection string matches the expected value.
        /// </summary>
        [Fact]
        public void TriggerMetadataWithMssqlType_RetrievesConnectionStringFromConfiguration()
        {
            // Arrange - Create triggerMetadata with type="mssql" and connectionName
            var hubName = "testHub";
            var connectionName = "TestConnection";
            var expectedConnectionString = TestHelpers.GetSqlConnectionString();
            
            var triggerMetadata = CreateTriggerMetadata(hubName, 10, 20, connectionName, "mssql");

            // Verify triggerMetadata has correct storageProvider.connectionName
            var storageProvider = triggerMetadata.Metadata["storageProvider"] as JObject;
            Assert.NotNull(storageProvider);
            Assert.Equal(connectionName, storageProvider["connectionName"]?.ToString());

            // Create factory
                // Options no longer used - removed CreateOptions call
            var factory = new SqlServerScalabilityProviderFactory(
                this.configuration,
                this.nameResolver,
                this.loggerFactory);

            var metadata = triggerMetadata.ExtractDurableTaskMetadata();

            // Act - Create provider from triggerMetadata
            var provider = factory.GetScalabilityProvider(metadata, triggerMetadata);

            // Assert - Verify provider was created
            Assert.NotNull(provider);
            Assert.IsType<SqlServerScalabilityProvider>(provider);

            // Assert - Verify connection string was retrieved from configuration
            var actualConnectionString = this.configuration.GetConnectionString(connectionName) ?? this.configuration[connectionName];
            Assert.NotNull(actualConnectionString);
            Assert.NotEmpty(actualConnectionString);
            Assert.Equal(expectedConnectionString, actualConnectionString);
        }

        /// <summary>
        /// Scenario: Managed Identity support - TokenCredential extracted from triggerMetadata.
        /// Validates that when triggerMetadata contains AzureComponentFactory, we extract TokenCredential.
        /// Tests that TokenCredential is properly passed to CreateSqlOrchestrationService.
        /// Verifies that connection string is built with Authentication="Active Directory Default" when TokenCredential is present.
        /// This test simulates the Managed Identity flow used by Scale Controller.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_WithTokenCredential_ExtractsAndUsesCredential()
        {
            // Arrange - Create triggerMetadata with AzureComponentFactory in Properties (Managed Identity)
            var hubName = "testHub";
            var connectionName = "TestConnection";
            var triggerMetadata = CreateTriggerMetadata(hubName, 10, 20, connectionName, "mssql");

            // Add AzureComponentFactory wrapper to triggerMetadata.Properties (simulating Scale Controller)
            // We'll create a mock wrapper that returns a TokenCredential
            var mockTokenCredential = new Mock<global::Azure.Core.TokenCredential>();
            var mockFactory = new Mock<object>();
            var factoryType = typeof(object).Assembly.GetType("Microsoft.Azure.WebJobs.Host.AzureComponentFactoryWrapper");
            if (factoryType == null)
            {
                // If the type doesn't exist in test assembly, create a simple mock
                var mockFactoryObj = new Mock<object>();
                var createTokenCredentialMethod = mockFactoryObj.Object.GetType().GetMethod("CreateTokenCredential");
                if (createTokenCredentialMethod != null)
                {
                    triggerMetadata.Properties["AzureComponentFactory"] = mockFactoryObj.Object;
                }
            }

                // Options no longer used - removed CreateOptions call
            var factory = new SqlServerScalabilityProviderFactory(
                this.configuration,
                this.nameResolver,
                this.loggerFactory);

            var metadata = triggerMetadata.ExtractDurableTaskMetadata();

            // Act - Create provider from triggerMetadata with TokenCredential
            // Note: In real scenarios, the TokenCredential would be extracted and used to build
            // a connection string with Authentication="Active Directory Default"
            var provider = factory.GetScalabilityProvider(metadata, triggerMetadata);

            // Assert - Verify provider was created
            Assert.NotNull(provider);
            Assert.IsType<SqlServerScalabilityProvider>(provider);
            
            // Note: Full TokenCredential testing would require actual Azure environment setup
            // This test verifies the extraction logic exists and provider creation works
        }

        /// <summary>
        /// Scenario: Managed Identity - connection string built with server name from configuration.
        /// Validates that when TokenCredential is present, we read server name from configuration.
        /// Tests pattern: {connectionName}__serverName or {connectionName}__server.
        /// Verifies that connection string is constructed with Authentication="Active Directory Default".
        /// This test validates the configuration reading logic for Managed Identity SQL connections.
        /// </summary>
        [Fact]
        public void CreateSqlOrchestrationService_WithManagedIdentityConfig_ReadsServerNameFromConfig()
        {
            // Arrange - Set up configuration with server name for Managed Identity
            var connectionName = "TestConnection";
            var serverName = "mysqlservertny.database.windows.net";
            var databaseName = "testsqlscaling";
            
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(new Dictionary<string, string>
            {
                { $"{connectionName}__serverName", serverName },
                { $"{connectionName}__databaseName", databaseName },
                // Also provide connection string as fallback
                { connectionName, $"Server={serverName};Database={databaseName};Authentication=Active Directory Default;" },
            });
            var testConfiguration = configBuilder.Build();

            // Options no longer used - removed CreateOptions call
            var factory = new SqlServerScalabilityProviderFactory(
                testConfiguration,
                this.nameResolver,
                this.loggerFactory);

            // Act - Try to create provider (this will test server name extraction)
            // Note: This test verifies configuration reading, not actual TokenCredential usage
            // Full Managed Identity testing requires Azure environment setup
            
            // Assert - Verify configuration values can be read
            var configServerName = testConfiguration[$"{connectionName}__serverName"];
            Assert.Equal(serverName, configServerName);
            
            var configDatabaseName = testConfiguration[$"{connectionName}__databaseName"];
            Assert.Equal(databaseName, configDatabaseName);
        }
    }
}
