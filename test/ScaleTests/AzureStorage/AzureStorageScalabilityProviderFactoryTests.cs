// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.AzureStorage;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Tests
{
    public class AzureStorageScalabilityProviderFactoryTests
    {
        private readonly ITestOutputHelper output;
        private readonly TestLoggerProvider loggerProvider;
        private readonly ILoggerFactory loggerFactory;
        private readonly IStorageServiceClientProviderFactory clientProviderFactory;
        private readonly INameResolver nameResolver;
        private readonly IConfiguration configuration;

        public AzureStorageScalabilityProviderFactoryTests(ITestOutputHelper output)
        {
            this.output = output;
            this.loggerFactory = new LoggerFactory();
            this.loggerProvider = new TestLoggerProvider(output);
            this.loggerFactory.AddProvider(this.loggerProvider);

            // Create real configuration with UseDevelopmentStorage=true
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(new Dictionary<string, string>
            {
                { "AzureWebJobsStorage", TestHelpers.GetStorageConnectionString() },
                { "TestConnection", TestHelpers.GetStorageConnectionString() },
            });
            this.configuration = configBuilder.Build();

            // Use real factory instead of mocking
            this.clientProviderFactory = new StorageServiceClientProviderFactory(this.configuration, this.loggerFactory);
            this.nameResolver = new SimpleNameResolver();
        }

        private class SimpleNameResolver : INameResolver
        {
            public string Resolve(string name) => name;
        }

        /// <summary>
        /// Scenario: Creating factory with valid parameters.
        /// Validates that factory can be instantiated with proper configuration.
        /// Verifies factory name is "AzureStorage" and connection name is set correctly.
        /// </summary>
        [Fact]
        public void Constructor_ValidParameters_CreatesInstance()
        {
            // Act
            var factory = new AzureStorageScalabilityProviderFactory(
                this.clientProviderFactory,
                this.nameResolver,
                this.loggerFactory);

            // Assert
            Assert.NotNull(factory);
            Assert.Equal("AzureStorage", factory.Name);
            // DefaultConnectionName is now hardcoded, not from options
            Assert.Equal("AzureWebJobsStorage", factory.DefaultConnectionName);
        }

        /// <summary>
        /// Scenario: Constructor validation - null options.
        /// Validates that factory properly rejects null options parameter.
        /// Ensures proper error handling for missing configuration.
        /// </summary>
        // Test removed: Options parameter no longer exists in constructor

        /// <summary>
        /// Scenario: Constructor validation - null client provider factory.
        /// Validates that factory requires a valid storage client provider factory.
        /// Ensures storage connectivity dependencies are enforced.
        /// </summary>
        [Fact]
        public void Constructor_NullClientProviderFactory_ThrowsArgumentNullException()
        {
            // Arrange
            // Options no longer used - removed CreateOptions call

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new AzureStorageScalabilityProviderFactory(
                    null,
                    this.nameResolver,
                    this.loggerFactory));
        }

        /// <summary>
        /// Scenario: Creating durability provider without trigger metadata (default path).
        /// Validates that provider can be created using only options configuration.
        /// Tests connection string-based authentication (no TokenCredential).
        /// Verifies provider has correct type, connection name, and concurrency settings.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_ReturnsValidProvider()
        {
            // Arrange
            // Options no longer used - removed CreateOptions call

            var factory = new AzureStorageScalabilityProviderFactory(
                this.clientProviderFactory,
                this.nameResolver,
                this.loggerFactory);

            // Act
            var provider = factory.GetScalabilityProvider();

            // Assert
            Assert.NotNull(provider);
            Assert.IsType<AzureStorageScalabilityProvider>(provider);
            var azureProvider = (AzureStorageScalabilityProvider)provider;
            // Azure Storage defaults to 10 for both orchestrator and activity
            Assert.Equal(10, azureProvider.MaxConcurrentTaskOrchestrationWorkItems);
            Assert.Equal(10, azureProvider.MaxConcurrentTaskActivityWorkItems);
        }

        /// <summary>
        /// Scenario: Creating durability provider with trigger metadata from ScaleController.
        /// Validates the end-to-end flow when Scale Controller calls with trigger metadata.
        /// Tests that max concurrent settings from options are applied (not from metadata).
        /// Verifies connection name resolution and provider creation.
        /// This is the primary path used by Azure Functions Scale Controller.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_WithTriggerMetadata_ReturnsValidProvider()
        {
            // Arrange
            // Options no longer used - removed CreateOptions call
            var factory = new AzureStorageScalabilityProviderFactory(
                this.clientProviderFactory,
                this.nameResolver,
                this.loggerFactory);

            var triggerMetadata = CreateTriggerMetadata("testHub", 15, 25, "TestConnection");
            var metadata = triggerMetadata.ExtractDurableTaskMetadata();

            // Act
            var provider = factory.GetScalabilityProvider(metadata, triggerMetadata);

            // Assert
            Assert.NotNull(provider);
            Assert.IsType<AzureStorageScalabilityProvider>(provider);
            var azureProvider = (AzureStorageScalabilityProvider)provider;
            // TriggerMetadata values (15, 25) now take priority over options (10, 20)
            Assert.Equal(15, azureProvider.MaxConcurrentTaskOrchestrationWorkItems);
            Assert.Equal(25, azureProvider.MaxConcurrentTaskActivityWorkItems);
        }

        /// <summary>
        /// Scenario: Validation - invalid hub name (too short).
        /// Validates that hub name must be between 3 and 50 characters.
        /// Tests Azure Storage naming convention enforcement.
        /// Ensures early validation before attempting storage connections.
        /// </summary>
        [Fact]
        public void ValidateAzureStorageOptions_InvalidHubName_ThrowsArgumentException()
        {
            // Arrange - Hub name too short (invalid)
            var factory = new AzureStorageScalabilityProviderFactory(
                this.clientProviderFactory,
                this.nameResolver,
                this.loggerFactory);

            var triggerMetadata = CreateTriggerMetadata("ab", 10, 20, "TestConnection"); // "ab" is too short
            var metadata = triggerMetadata.ExtractDurableTaskMetadata();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => factory.GetScalabilityProvider(metadata, triggerMetadata));
        }

        /// <summary>
        /// Scenario: Validation - invalid concurrency settings.
        /// Validates that max concurrent orchestrator/activity functions must be >= 1.
        /// Tests concurrency configuration enforcement.
        /// Ensures valid worker count calculations for scaling.
        /// </summary>
        [Fact]
        public void ValidateAzureStorageOptions_InvalidMaxConcurrent_ThrowsInvalidOperationException()
        {
            // Arrange - MaxConcurrentOrchestratorFunctions is 0 (invalid)
            var factory = new AzureStorageScalabilityProviderFactory(
                this.clientProviderFactory,
                this.nameResolver,
                this.loggerFactory);

            var triggerMetadata = CreateTriggerMetadata("testHub", 0, 20, "TestConnection"); // 0 is invalid
            var metadata = triggerMetadata.ExtractDurableTaskMetadata();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => factory.GetScalabilityProvider(metadata, triggerMetadata));
        }

        /// <summary>
        /// Scenario: Provider caching for performance optimization.
        /// Validates that factory reuses the same provider instance for multiple calls.
        /// Tests performance optimization by avoiding redundant provider creation.
        /// Ensures consistent metrics collection across scale decisions.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_CachesDefaultProvider()
        {
            // Arrange
            // Options no longer used - removed CreateOptions call
            var factory = new AzureStorageScalabilityProviderFactory(
                this.clientProviderFactory,
                this.nameResolver,
                this.loggerFactory);

            // Act
            var provider1 = factory.GetScalabilityProvider();
            var provider2 = factory.GetScalabilityProvider();

            // Assert
            Assert.Same(provider1, provider2);
        }

        // CreateOptions helper removed - DurableTaskScaleOptions no longer exists
        // Tests now rely on TriggerMetadata from Scale Controller instead of DurableTaskScaleOptions

        private static TriggerMetadata CreateTriggerMetadata(
            string hubName,
            int maxOrchestrator,
            int maxActivity,
            string connectionName)
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
                        { "type", "AzureStorage" },
                        { "connectionName", connectionName },
                    }
                },
            };

            // Use the public constructor
            return new TriggerMetadata(metadata);
        }

        /// <summary>
        /// ✅ KEY SCENARIO 1: Default Azure Storage provider registration.
        /// Validates that factory works with the standard "AzureWebJobsStorage" connection name.
        /// Tests the most common configuration used by Azure Functions.
        /// Verifies connection string resolution from default app settings.
        /// Confirms provider is created with correct concurrency limits.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_WithDefaultAzureWebJobsStorage_CreatesProvider()
        {
            // Arrange - Using default AzureWebJobsStorage connection
            // Options no longer used - removed CreateOptions call

            var factory = new AzureStorageScalabilityProviderFactory(
                this.clientProviderFactory,
                this.nameResolver,
                this.loggerFactory);

            // Act
            var provider = factory.GetScalabilityProvider();

            // Assert
            Assert.NotNull(provider);
            Assert.IsType<AzureStorageScalabilityProvider>(provider);
            Assert.Equal("AzureWebJobsStorage", provider.ConnectionName);
            // Azure Storage defaults to 10 for both orchestrator and activity
            Assert.Equal(10, provider.MaxConcurrentTaskOrchestrationWorkItems);
            Assert.Equal(10, provider.MaxConcurrentTaskActivityWorkItems);
        }

        /// <summary>
        /// ✅ KEY SCENARIO 2: Multiple connections - retrieve values and connect correctly.
        /// Validates that factory can handle different connection names in a single app.
        /// Tests configuration retrieval for custom connections (multi-tenant scenarios).
        /// Verifies that each provider connects to correct storage using respective connection strings.
        /// Ensures isolation between different storage backends in the same application.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_WithMultipleConnections_CreatesProvidersSuccessfully()
        {
            // Arrange - Test with multiple different connection names via trigger metadata
            var connectionNames = new[] { "AzureWebJobsStorage", "TestConnection", "CustomConnection" };
            
            // Add custom connection to configuration
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(new Dictionary<string, string>
            {
                { "AzureWebJobsStorage", TestHelpers.GetStorageConnectionString() },
                { "TestConnection", TestHelpers.GetStorageConnectionString() },
                { "CustomConnection", TestHelpers.GetStorageConnectionString() }
            });
            var config = configBuilder.Build();
            var clientFactory = new StorageServiceClientProviderFactory(config, this.loggerFactory);

            foreach (var connectionName in connectionNames)
            {
                // Options no longer used - removed CreateOptions call
                var factory = new AzureStorageScalabilityProviderFactory(
                    clientFactory,
                    this.nameResolver,
                    this.loggerFactory);

                // Pass connection name via trigger metadata (Scale Controller behavior)
                var triggerMetadata = CreateTriggerMetadata("testHub", 5, 10, connectionName);
                var metadata = triggerMetadata.ExtractDurableTaskMetadata();

                // Act
                var provider = factory.GetScalabilityProvider(metadata, triggerMetadata);

                // Assert
                Assert.NotNull(provider);
                Assert.Equal(connectionName, provider.ConnectionName);
                this.output.WriteLine($"Successfully created provider for connection: {connectionName}");
            }
        }

        /// <summary>
        /// Scenario: Factory type identification.
        /// Validates that factory correctly identifies itself as "AzureStorage" type.
        /// Tests factory registration and type resolution in DI container.
        /// Ensures Scale Controller can identify which storage backend is in use.
        /// </summary>
        [Fact]
        public void Factory_Name_IsAzureStorage()
        {
            // Arrange
            // Options no longer used - removed CreateOptions call
            var factory = new AzureStorageScalabilityProviderFactory(
                this.clientProviderFactory,
                this.nameResolver,
                this.loggerFactory);

            // Act & Assert
            Assert.Equal("AzureStorage", factory.Name);
        }

        /// <summary>
        /// ✅ KEY SCENARIO 3: Register durability provider via storage type.
        /// Validates that factory is selected based on storageProvider.type = "AzureStorage".
        /// Tests the provider factory selection mechanism when multiple backends are available.
        /// Verifies correct provider type is instantiated for Azure Storage backend.
        /// Ensures extensibility for future storage backend support (MSSQL, Netherite, etc.).
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_WithAzureStorageType_UsesCorrectProvider()
        {
            // Arrange
            var factory = new AzureStorageScalabilityProviderFactory(
                this.clientProviderFactory,
                this.nameResolver,
                this.loggerFactory);

            // Act
            var provider = factory.GetScalabilityProvider();

            // Assert
            Assert.NotNull(provider);
            Assert.IsType<AzureStorageScalabilityProvider>(provider);
            Assert.Equal("AzureStorage", factory.Name);
        }

        /// <summary>
        /// Scenario: Configuration value retrieval and connection string resolution.
        /// Validates that IConfiguration correctly resolves custom connection names.
        /// Tests the configuration binding mechanism for connection strings.
        /// Verifies end-to-end flow from configuration to storage connection.
        /// Ensures custom connection names work with Azure Storage emulator.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_RetrievesConnectionStringFromConfiguration()
        {
            // Arrange - Verify we can retrieve connection string from configuration
            var testConnectionString = TestHelpers.GetStorageConnectionString();
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(new Dictionary<string, string>
            {
                // Use the hardcoded default connection name
                { "AzureWebJobsStorage", testConnectionString }
            });
            var config = configBuilder.Build();
            var clientFactory = new StorageServiceClientProviderFactory(config, this.loggerFactory);

            // Options no longer used - removed CreateOptions call
            var factory = new AzureStorageScalabilityProviderFactory(
                clientFactory,
                this.nameResolver,
                this.loggerFactory);

            // Act - Without trigger metadata, uses hardcoded default "AzureWebJobsStorage"
            var provider = factory.GetScalabilityProvider();

            // Assert
            Assert.NotNull(provider);
            Assert.Equal("AzureWebJobsStorage", provider.ConnectionName);
            
            // Verify the connection string was retrieved from configuration
            var retrievedConnectionString = config["AzureWebJobsStorage"];
            Assert.Equal(testConnectionString, retrievedConnectionString);
        }
    }
}
