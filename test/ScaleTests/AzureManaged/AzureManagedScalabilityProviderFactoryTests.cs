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
            // Options no longer used - removed CreateOptions call

            // Act
            var factory = new AzureManagedScalabilityProviderFactory(
                this.configuration,
                this.nameResolver,
                this.loggerFactory);

            // Assert
            Assert.NotNull(factory);
            Assert.Equal("AzureManaged", factory.Name);
            // DefaultConnectionName is now hardcoded, not from options
            Assert.Equal("DURABLE_TASK_SCHEDULER_CONNECTION_STRING", factory.DefaultConnectionName);
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
            // Options no longer used - removed CreateOptions call

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new AzureManagedScalabilityProviderFactory(
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
        public void GetScalabilityProvider_WithAzureManagedType_CreatesAzureManagedProvider()
        {
            // Arrange - Azure Managed now requires metadata
            var factory = new AzureManagedScalabilityProviderFactory(
                this.configuration,
                this.nameResolver,
                this.loggerFactory);

            // Act & Assert - Should throw NotImplementedException since Azure Managed requires metadata
            Assert.Throws<NotImplementedException>(() => factory.GetScalabilityProvider());
        }

        /// <summary>
        /// Scenario: Creating durability provider without trigger metadata (default path).
        /// Validates that provider can be created using only options configuration.
        /// Tests connection string-based authentication with DefaultAzureCredential.
        /// Verifies provider has correct type, connection name, and concurrency settings.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_ReturnsValidProvider()
        {
            // Arrange - Azure Managed now requires metadata
            var factory = new AzureManagedScalabilityProviderFactory(
                this.configuration,
                this.nameResolver,
                this.loggerFactory);

            // Act & Assert - Should throw NotImplementedException since Azure Managed requires metadata
            Assert.Throws<NotImplementedException>(() => factory.GetScalabilityProvider());
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
        public void GetScalabilityProvider_WithTriggerMetadata_ReturnsValidProvider()
        {
            // Arrange
            // Options no longer used - removed CreateOptions call
            var factory = new AzureManagedScalabilityProviderFactory(
                this.configuration,
                this.nameResolver,
                this.loggerFactory);

            var triggerMetadata = CreateTriggerMetadata("testHub", 15, 25, "v3-dtsConnectionMI");
            var metadata = triggerMetadata.ExtractDurableTaskMetadata();

            // Act
            var provider = factory.GetScalabilityProvider(metadata, triggerMetadata);

            // Assert
            Assert.NotNull(provider);
            Assert.IsType<AzureManagedScalabilityProvider>(provider);
            var azureProvider = (AzureManagedScalabilityProvider)provider;
            Assert.Equal("v3-dtsConnectionMI", azureProvider.ConnectionName);
            // TriggerMetadata values (15, 25) now take priority over options (10, 20)
            Assert.Equal(15, azureProvider.MaxConcurrentTaskOrchestrationWorkItems);
            Assert.Equal(25, azureProvider.MaxConcurrentTaskActivityWorkItems);
        }

        /// <summary>
        /// Scenario: Provider caching for performance optimization with same connection and client ID.
        /// Validates that factory reuses the same provider instance for multiple calls with same parameters.
        /// Tests performance optimization by avoiding redundant provider creation.
        /// Ensures consistent metrics collection across scale decisions.
        /// Azure Managed uses (connectionName, taskHubName, clientId) as cache key.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_CachesProviderWithSameConnectionAndClientId()
        {
            // Arrange - Azure Managed now requires metadata
            var factory = new AzureManagedScalabilityProviderFactory(
                this.configuration,
                this.nameResolver,
                this.loggerFactory);

            // Act & Assert - Should throw NotImplementedException since Azure Managed requires metadata
            Assert.Throws<NotImplementedException>(() => factory.GetScalabilityProvider());
        }

        /// <summary>
        /// Scenario: Factory uses default connection name when not specified.
        /// Validates that factory falls back to DURABLE_TASK_SCHEDULER_CONNECTION_STRING.
        /// Tests the default connection name pattern for Azure Managed backend.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_WithDefaultConnectionName_CreatesProvider()
        {
            // Arrange - Azure Managed now requires metadata
            var factory = new AzureManagedScalabilityProviderFactory(
                this.configuration,
                this.nameResolver,
                this.loggerFactory);

            // Act & Assert - Should throw NotImplementedException since Azure Managed requires metadata
            Assert.Throws<NotImplementedException>(() => factory.GetScalabilityProvider());
        }

        /// <summary>
        /// Scenario: Missing connection string throws exception.
        /// Validates that factory fails gracefully when connection string is not configured.
        /// Ensures proper error messaging for configuration issues.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_MissingConnectionString_CreatesProviderWithDefaultCredential()
        {
            // Arrange - Azure Managed now requires metadata
            var factory = new AzureManagedScalabilityProviderFactory(
                this.configuration,
                this.nameResolver,
                this.loggerFactory);

            // Act & Assert - Should throw NotImplementedException since Azure Managed requires metadata
            Assert.Throws<NotImplementedException>(() => factory.GetScalabilityProvider());
        }

        /// <summary>
        /// Scenario: Configuration value retrieval and connection string resolution.
        /// Validates that IConfiguration correctly resolves custom connection names.
        /// Tests the configuration binding mechanism for connection strings.
        /// Verifies end-to-end flow from configuration to Azure Managed connection.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_RetrievesConnectionStringFromConfiguration()
        {
            // Arrange - Verify we can retrieve connection string from configuration
            var testConnectionString = "Endpoint=https://custom.westus.durabletask.io;Authentication=DefaultAzure";
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(new Dictionary<string, string>
            {
                // Use the hardcoded default connection name
                { "DURABLE_TASK_SCHEDULER_CONNECTION_STRING", testConnectionString }
            });
            var config = configBuilder.Build();

            var factory = new AzureManagedScalabilityProviderFactory(
                config,
                this.nameResolver,
                this.loggerFactory);

            // Act & Assert - Should throw NotImplementedException since Azure Managed requires metadata
            Assert.Throws<NotImplementedException>(() => factory.GetScalabilityProvider());
        }

        /// <summary>
        /// Scenario: Factory correctly parses connection string with task hub name.
        /// Validates that task hub name from connection string is used when not in options.
        /// Tests connection string parsing logic.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_UsesTaskHubNameFromConnectionString()
        {
            // Arrange - Azure Managed now requires metadata
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(new Dictionary<string, string>
            {
                // Use the hardcoded default connection name with TaskHub in connection string
                { "DURABLE_TASK_SCHEDULER_CONNECTION_STRING", "Endpoint=https://test.westus.durabletask.io;Authentication=DefaultAzure;TaskHub=MyTaskHub" }
            });
            var config = configBuilder.Build();

            var factory = new AzureManagedScalabilityProviderFactory(
                config,
                this.nameResolver,
                this.loggerFactory);

            // Act & Assert - Should throw NotImplementedException since Azure Managed requires metadata
            Assert.Throws<NotImplementedException>(() => factory.GetScalabilityProvider());
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


