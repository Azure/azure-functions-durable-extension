// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.AzureStorage;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Tests
{
    public class AzureStorageScalabilityProviderFactoryTests
    {
        private readonly ITestOutputHelper output;
        private readonly TestLoggerProvider loggerProvider;
        private readonly ILoggerFactory loggerFactory;
        private readonly IStorageServiceClientProviderFactory clientProviderFactory;
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
                this.loggerFactory);

            // Assert
            Assert.NotNull(factory);
            Assert.Equal("AzureStorage", factory.Name);

            // DefaultConnectionName is now hardcoded, not from options
            Assert.Equal("AzureWebJobsStorage", factory.DefaultConnectionName);
        }

        /// <summary>
        /// Scenario: Constructor validation - null client provider factory.
        /// Validates that factory requires a valid storage client provider factory.
        /// Ensures storage connectivity dependencies are enforced.
        /// </summary>
        [Fact]
        public void Constructor_NullClientProviderFactory_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new AzureStorageScalabilityProviderFactory(
                    null,
                    this.loggerFactory));
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
                this.loggerFactory);

            var triggerMetadata = CreateTriggerMetadata("testHub", 0, 20, "TestConnection"); // 0 is invalid
            var metadata = triggerMetadata.ExtractDurableTaskMetadata();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => factory.GetScalabilityProvider(metadata, triggerMetadata));
        }

        /// <summary>
        /// Test that when multiple connections are provided, validat factory can handle different connection names correctly.
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
                { "CustomConnection", TestHelpers.GetStorageConnectionString() },
            });
            var config = configBuilder.Build();
            var clientFactory = new StorageServiceClientProviderFactory(config, this.loggerFactory);

            foreach (var connectionName in connectionNames)
            {
                // Options no longer used - removed CreateOptions call
                var factory = new AzureStorageScalabilityProviderFactory(
                    clientFactory,
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

        // Helper method to simulate trigger metadata passed from scale controller.
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
            return new TriggerMetadata(metadata);
        }
    }
}
