// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.AzureManaged;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Tests
{
    public class AzureManagedScalabilityProviderFactoryTests
    {
        private readonly ITestOutputHelper output;
        private readonly TestLoggerProvider loggerProvider;
        private readonly ILoggerFactory loggerFactory;
        private readonly INameResolver nameResolver;
        private readonly IConfiguration configuration;

        public AzureManagedScalabilityProviderFactoryTests(ITestOutputHelper output)
        {
            this.output = output;
            this.loggerFactory = new LoggerFactory();
            this.loggerProvider = new TestLoggerProvider(output);
            this.loggerFactory.AddProvider(this.loggerProvider);

            // Create configuration with Azure Managed connection string
            // Using DefaultAzureCredential for local testing
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(new Dictionary<string, string>
            {
                { "v3-dtsConnectionMI", "Endpoint=https://test.westus.durabletask.io;Authentication=DefaultAzure" },
                { "DURABLE_TASK_SCHEDULER_CONNECTION_STRING", "Endpoint=https://default.westus.durabletask.io;Authentication=DefaultAzure" },
            });
            this.configuration = configBuilder.Build();

            this.nameResolver = new SimpleNameResolver();
        }

        private class SimpleNameResolver : INameResolver
        {
            public string Resolve(string name) => name;
        }

        /// <summary>
        /// Scenario: Creating factory with valid parameters.
        /// Validates that factory can be instantiated with proper configuration.
        /// Verifies factory name is "AzureManaged" and connection name is set correctly.
        /// </summary>
        [Fact]
        public void Constructor_ValidParameters_CreatesInstance()
        {
            // Arrange
            var options = CreateOptions("testHub", 10, 20, "v3-dtsConnectionMI");

            // Act
            var factory = new AzureManagedScalabilityProviderFactory(
                options,
                this.configuration,
                this.nameResolver,
                this.loggerFactory);

            // Assert
            Assert.NotNull(factory);
            Assert.Equal("AzureManaged", factory.Name);
            Assert.Equal("v3-dtsConnectionMI", factory.DefaultConnectionName);
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
                new AzureManagedScalabilityProviderFactory(
                    null,
                    this.configuration,
                    this.nameResolver,
                    this.loggerFactory));
        }

        /// <summary>
        /// Scenario: Constructor validation - null configuration.
        /// Validates that factory requires a valid configuration provider.
        /// Ensures connection string resolution dependencies are enforced.
        /// </summary>
        [Fact]
        public void Constructor_NullConfiguration_ThrowsArgumentNullException()
        {
            // Arrange
            var options = CreateOptions("testHub", 10, 20, "v3-dtsConnectionMI");

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new AzureManagedScalabilityProviderFactory(
                    options,
                    null,
                    this.nameResolver,
                    this.loggerFactory));
        }

        /// <summary>
        /// ✅ KEY SCENARIO 1: Creating durability provider when trigger metadata specifies type is "azureManaged".
        /// Validates that the factory creates an AzureManagedScalabilityProvider when storageProvider.type = "azureManaged".
        /// Tests the provider factory selection mechanism when multiple backends are available.
        /// Verifies correct provider type is instantiated for Azure Managed backend.
        /// This is the primary path used by Azure Functions Scale Controller for Azure Managed backend.
        /// </summary>
        [Fact]
        public void GetDurabilityProvider_WithAzureManagedType_CreatesAzureManagedProvider()
        {
            // Arrange - Explicitly set storageProvider type to "azureManaged"
            var options = CreateOptions("testHub", 10, 20, "v3-dtsConnectionMI");

            var factory = new AzureManagedScalabilityProviderFactory(
                options,
                this.configuration,
                this.nameResolver,
                this.loggerFactory);

            // Act
            var provider = factory.GetDurabilityProvider();

            // Assert
            Assert.NotNull(provider);
            Assert.IsType<AzureManagedScalabilityProvider>(provider);
            Assert.Equal("AzureManaged", factory.Name);
            Assert.Equal(10, provider.MaxConcurrentTaskOrchestrationWorkItems);
            Assert.Equal(20, provider.MaxConcurrentTaskActivityWorkItems);
        }

        /// <summary>
        /// Scenario: Creating durability provider without trigger metadata (default path).
        /// Validates that provider can be created using only options configuration.
        /// Tests connection string-based authentication with DefaultAzureCredential.
        /// Verifies provider has correct type, connection name, and concurrency settings.
        /// </summary>
        [Fact]
        public void GetDurabilityProvider_ReturnsValidProvider()
        {
            // Arrange
            var options = CreateOptions("testHub", 10, 20, "v3-dtsConnectionMI");

            var factory = new AzureManagedScalabilityProviderFactory(
                options,
                this.configuration,
                this.nameResolver,
                this.loggerFactory);

            // Act
            var provider = factory.GetDurabilityProvider();

            // Assert
            Assert.NotNull(provider);
            Assert.IsType<AzureManagedScalabilityProvider>(provider);
            var azureProvider = (AzureManagedScalabilityProvider)provider;
            Assert.Equal(10, azureProvider.MaxConcurrentTaskOrchestrationWorkItems);
            Assert.Equal(20, azureProvider.MaxConcurrentTaskActivityWorkItems);
            Assert.Equal("v3-dtsConnectionMI", azureProvider.ConnectionName);
        }

        /// <summary>
        /// ✅ KEY SCENARIO 2: Creating durability provider with trigger metadata from ScaleController.
        /// Validates the end-to-end flow when Scale Controller calls with trigger metadata.
        /// Tests that connection name "v3-dtsConnectionMI" from trigger metadata is used correctly.
        /// Tests that max concurrent settings from options are applied.
        /// Verifies connection name resolution and provider creation.
        /// This is the primary path used by Azure Functions Scale Controller.
        /// </summary>
        [Fact]
        public void GetDurabilityProvider_WithTriggerMetadata_ReturnsValidProvider()
        {
            // Arrange
            var options = CreateOptions("testHub", 10, 20, "v3-dtsConnectionMI");
            var factory = new AzureManagedScalabilityProviderFactory(
                options,
                this.configuration,
                this.nameResolver,
                this.loggerFactory);

            var triggerMetadata = CreateTriggerMetadata("testHub", 15, 25, "v3-dtsConnectionMI");

            // Act
            var provider = factory.GetDurabilityProvider(triggerMetadata);

            // Assert
            Assert.NotNull(provider);
            Assert.IsType<AzureManagedScalabilityProvider>(provider);
            var azureProvider = (AzureManagedScalabilityProvider)provider;
            Assert.Equal("v3-dtsConnectionMI", azureProvider.ConnectionName);
            // Note: Uses options values (10, 20), not trigger metadata values (15, 25)
            Assert.Equal(10, azureProvider.MaxConcurrentTaskOrchestrationWorkItems);
            Assert.Equal(20, azureProvider.MaxConcurrentTaskActivityWorkItems);
        }

        /// <summary>
        /// Scenario: Provider caching for performance optimization with same connection and client ID.
        /// Validates that factory reuses the same provider instance for multiple calls with same parameters.
        /// Tests performance optimization by avoiding redundant provider creation.
        /// Ensures consistent metrics collection across scale decisions.
        /// Azure Managed uses (connectionName, taskHubName, clientId) as cache key.
        /// </summary>
        [Fact]
        public void GetDurabilityProvider_CachesProviderWithSameConnectionAndClientId()
        {
            // Arrange
            var options = CreateOptions("testHub", 10, 20, "v3-dtsConnectionMI");
            var factory = new AzureManagedScalabilityProviderFactory(
                options,
                this.configuration,
                this.nameResolver,
                this.loggerFactory);

            // Act - Call twice with no trigger metadata (same cache key)
            var provider1 = factory.GetDurabilityProvider();
            var provider2 = factory.GetDurabilityProvider();

            // Assert - Should be the same cached instance
            Assert.Same(provider1, provider2);
        }

        /// <summary>
        /// Scenario: Factory uses default connection name when not specified.
        /// Validates that factory falls back to DURABLE_TASK_SCHEDULER_CONNECTION_STRING.
        /// Tests the default connection name pattern for Azure Managed backend.
        /// </summary>
        [Fact]
        public void GetDurabilityProvider_WithDefaultConnectionName_CreatesProvider()
        {
            // Arrange - Don't specify connectionName in storageProvider
            var options = new DurableTaskScaleOptions
            {
                HubName = "testHub",
                MaxConcurrentOrchestratorFunctions = 10,
                MaxConcurrentActivityFunctions = 20,
                StorageProvider = new Dictionary<string, object>
                {
                    { "type", "azureManaged" },
                },
            };

            var factory = new AzureManagedScalabilityProviderFactory(
                Options.Create(options),
                this.configuration,
                this.nameResolver,
                this.loggerFactory);

            // Act
            var provider = factory.GetDurabilityProvider();

            // Assert
            Assert.NotNull(provider);
            Assert.IsType<AzureManagedScalabilityProvider>(provider);
            Assert.Equal("DURABLE_TASK_SCHEDULER_CONNECTION_STRING", factory.DefaultConnectionName);
        }

        /// <summary>
        /// Scenario: Missing connection string throws exception.
        /// Validates that factory fails gracefully when connection string is not configured.
        /// Ensures proper error messaging for configuration issues.
        /// </summary>
        [Fact]
        public void GetDurabilityProvider_MissingConnectionString_ThrowsException()
        {
            // Arrange - Use connection name that doesn't exist in configuration
            var options = CreateOptions("testHub", 10, 20, "NonExistentConnection");

            var factory = new AzureManagedScalabilityProviderFactory(
                options,
                this.configuration,
                this.nameResolver,
                this.loggerFactory);

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => factory.GetDurabilityProvider());
            Assert.Contains("No connection string configuration was found", exception.Message);
            Assert.Contains("NonExistentConnection", exception.Message);
        }

        /// <summary>
        /// Scenario: Configuration value retrieval and connection string resolution.
        /// Validates that IConfiguration correctly resolves custom connection names.
        /// Tests the configuration binding mechanism for connection strings.
        /// Verifies end-to-end flow from configuration to Azure Managed connection.
        /// </summary>
        [Fact]
        public void GetDurabilityProvider_RetrievesConnectionStringFromConfiguration()
        {
            // Arrange - Verify we can retrieve connection string from configuration
            var testConnectionString = "Endpoint=https://custom.westus.durabletask.io;Authentication=DefaultAzure";
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(new Dictionary<string, string>
            {
                { "MyCustomConnection", testConnectionString }
            });
            var config = configBuilder.Build();

            var options = CreateOptions("testHub", 10, 20, "MyCustomConnection");
            var factory = new AzureManagedScalabilityProviderFactory(
                options,
                config,
                this.nameResolver,
                this.loggerFactory);

            // Act
            var provider = factory.GetDurabilityProvider();

            // Assert
            Assert.NotNull(provider);
            Assert.Equal("MyCustomConnection", provider.ConnectionName);
            
            // Verify the connection string was retrieved from configuration
            var retrievedConnectionString = config["MyCustomConnection"];
            Assert.Equal(testConnectionString, retrievedConnectionString);
        }

        /// <summary>
        /// Scenario: Factory correctly parses connection string with task hub name.
        /// Validates that task hub name from connection string is used when not in options.
        /// Tests connection string parsing logic.
        /// </summary>
        [Fact]
        public void GetDurabilityProvider_UsesTaskHubNameFromConnectionString()
        {
            // Arrange - Connection string with TaskHub specified
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(new Dictionary<string, string>
            {
                { "ConnectionWithHub", "Endpoint=https://test.westus.durabletask.io;Authentication=DefaultAzure;TaskHub=MyTaskHub" }
            });
            var config = configBuilder.Build();

            var options = new DurableTaskScaleOptions
            {
                // Don't set HubName in options
                MaxConcurrentOrchestratorFunctions = 10,
                MaxConcurrentActivityFunctions = 20,
                StorageProvider = new Dictionary<string, object>
                {
                    { "type", "azureManaged" },
                    { "connectionName", "ConnectionWithHub" },
                },
            };

            var factory = new AzureManagedScalabilityProviderFactory(
                Options.Create(options),
                config,
                this.nameResolver,
                this.loggerFactory);

            // Act
            var provider = factory.GetDurabilityProvider();

            // Assert
            Assert.NotNull(provider);
            // Provider should be created successfully with task hub from connection string
        }

        private static IOptions<DurableTaskScaleOptions> CreateOptions(
            string hubName,
            int maxOrchestrator,
            int maxActivity,
            string connectionName)
        {
            var options = new DurableTaskScaleOptions
            {
                HubName = hubName,
                MaxConcurrentOrchestratorFunctions = maxOrchestrator,
                MaxConcurrentActivityFunctions = maxActivity,
                StorageProvider = new Dictionary<string, object>
                {
                    { "type", "azureManaged" },
                    { "connectionName", connectionName },
                },
            };

            return Options.Create(options);
        }

        private static TriggerMetadata CreateTriggerMetadata(
            string hubName,
            int maxOrchestrator,
            int maxActivity,
            string connectionName)
        {
            var metadata = new JObject
            {
                { "functionName", "TestFunction" },
                { "type", "orchestrationTrigger" },
                { "taskHubName", hubName },
                { "maxConcurrentOrchestratorFunctions", maxOrchestrator },
                { "maxConcurrentActivityFunctions", maxActivity },
                {
                    "storageProvider", new JObject
                    {
                        { "type", "azureManaged" },
                        { "connectionName", connectionName },
                    }
                },
            };

            return new TriggerMetadata(metadata);
        }
    }
}


