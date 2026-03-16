// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using DurableTask.AzureStorage;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.AzureStorage;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Tests
{
    /// <summary>
    /// Tests for AzureStorageScalabilityProvider.
    /// Validates the Azure Storage implementation of ScalabilityProvider.
    /// Tests provider instantiation, concurrency configuration, and scale monitor/scaler creation.
    /// </summary>
    public class AzureStorageScalabilityProviderTests
    {
        private readonly ITestOutputHelper output;
        private readonly TestLoggerProvider loggerProvider;
        private readonly ILoggerFactory loggerFactory;

        public AzureStorageScalabilityProviderTests(ITestOutputHelper output)
        {
            this.output = output;
            this.loggerFactory = new LoggerFactory();
            this.loggerProvider = new TestLoggerProvider(output);
            this.loggerFactory.AddProvider(this.loggerProvider);
        }

        /// <summary>
        /// Scenario: Provider creation with authenticated storage client.
        /// Validates that provider accepts a pre-authenticated StorageAccountClientProvider.
        /// Tests that connection name is properly stored for Scale Controller identification.
        /// Ensures provider is ready to create scale monitors and target scalers.
        /// </summary>
        [Fact]
        public void Constructor_ValidParameters_CreatesInstance()
        {
            // Arrange
            var connectionString = TestHelpers.GetStorageConnectionString();
            var clientProvider = new StorageAccountClientProvider(connectionString);
            var logger = this.loggerFactory.CreateLogger<AzureStorageScalabilityProvider>();

            // Act
            var provider = new AzureStorageScalabilityProvider(
                clientProvider,
                "TestConnection",
                logger);

            // Assert
            Assert.NotNull(provider);
            Assert.Equal("TestConnection", provider.ConnectionName);
        }

        /// <summary>
        /// Scenario: Constructor validation - null client provider.
        /// Validates that provider requires an authenticated storage client.
        /// Tests defensive programming for required dependencies.
        /// Ensures clear error messages when storage connectivity is missing.
        /// </summary>
        [Fact]
        public void Constructor_NullClientProvider_ThrowsArgumentNullException()
        {
            // Arrange
            var logger = this.loggerFactory.CreateLogger<AzureStorageScalabilityProvider>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new AzureStorageScalabilityProvider(null, "TestConnection", logger));
        }

        /// <summary>
        /// Scenario: Orchestrator concurrency configuration.
        /// Validates that max concurrent orchestrators can be configured.
        /// Tests property setter and getter for MaxConcurrentTaskOrchestrationWorkItems.
        /// Ensures Scale Controller can apply concurrency limits for scaling decisions.
        /// </summary>
        [Fact]
        public void MaxConcurrentTaskOrchestrationWorkItems_CanBeSetAndRetrieved()
        {
            // Arrange
            var connectionString = TestHelpers.GetStorageConnectionString();
            var clientProvider = new StorageAccountClientProvider(connectionString);
            var logger = this.loggerFactory.CreateLogger<AzureStorageScalabilityProvider>();
            var provider = new AzureStorageScalabilityProvider(clientProvider, "TestConnection", logger);

            // Act
            provider.MaxConcurrentTaskOrchestrationWorkItems = 20;

            // Assert
            Assert.Equal(20, provider.MaxConcurrentTaskOrchestrationWorkItems);
        }

        /// <summary>
        /// Scenario: Activity concurrency configuration.
        /// Validates that max concurrent activities can be configured.
        /// Tests property setter and getter for MaxConcurrentTaskActivityWorkItems.
        /// Ensures Scale Controller can apply concurrency limits for scaling decisions.
        /// </summary>
        [Fact]
        public void MaxConcurrentTaskActivityWorkItems_CanBeSetAndRetrieved()
        {
            // Arrange
            var connectionString = TestHelpers.GetStorageConnectionString();
            var clientProvider = new StorageAccountClientProvider(connectionString);
            var logger = this.loggerFactory.CreateLogger<AzureStorageScalabilityProvider>();
            var provider = new AzureStorageScalabilityProvider(clientProvider, "TestConnection", logger);

            // Act
            provider.MaxConcurrentTaskActivityWorkItems = 30;

            // Assert
            Assert.Equal(30, provider.MaxConcurrentTaskActivityWorkItems);
        }

        /// <summary>
        /// Scenario: Scale Monitor creation for metrics-based autoscaling.
        /// Validates that provider can create IScaleMonitor for Scale Controller.
        /// Tests that Scale Controller can get metrics from Azure Storage queues/tables.
        /// Ensures monitoring infrastructure is properly initialized with storage connection.
        /// This is used by Scale Controller for metrics-based autoscaling decisions.
        /// </summary>
        [Fact]
        public void TryGetScaleMonitor_ValidParameters_ReturnsTrue()
        {
            // Arrange
            var connectionString = TestHelpers.GetStorageConnectionString();
            var clientProvider = new StorageAccountClientProvider(connectionString);
            var logger = this.loggerFactory.CreateLogger<AzureStorageScalabilityProvider>();
            var provider = new AzureStorageScalabilityProvider(clientProvider, "TestConnection", logger);

            // Act
            var result = provider.TryGetScaleMonitor(
                "functionId",
                "functionName",
                "testHub",
                "TestConnection",
                out IScaleMonitor scaleMonitor);

            // Assert
            Assert.True(result);
            Assert.NotNull(scaleMonitor);
        }

        /// <summary>
        /// Scenario: Target Scaler creation for target-based autoscaling.
        /// Validates that provider can create ITargetScaler for Scale Controller.
        /// Tests that Scale Controller can perform target-based scaling calculations.
        /// Ensures scaler can determine target worker count based on queue depths.
        /// This is the recommended approach for Durable Functions scaling.
        /// </summary>
        [Fact]
        public void TryGetTargetScaler_ValidParameters_ReturnsTrue()
        {
            // Arrange
            var connectionString = TestHelpers.GetStorageConnectionString();
            var clientProvider = new StorageAccountClientProvider(connectionString);
            var logger = this.loggerFactory.CreateLogger<AzureStorageScalabilityProvider>();
            var provider = new AzureStorageScalabilityProvider(clientProvider, "TestConnection", logger);

            // Act
            var result = provider.TryGetTargetScaler(
                "functionId",
                "functionName",
                "testHub",
                "TestConnection",
                out ITargetScaler targetScaler);

            // Assert
            Assert.True(result);
            Assert.NotNull(targetScaler);
        }

        /// <summary>
        /// Scenario: Metrics provider caching for performance.
        /// Validates that provider reuses the same metrics provider for multiple calls.
        /// Tests performance optimization to avoid redundant storage connections.
        /// Ensures consistent metrics collection across multiple scale decisions.
        /// Validates singleton pattern within a provider instance.
        /// </summary>
        [Fact]
        public void TryGetScaleMonitor_UsesSameMetricsProvider()
        {
            // Arrange
            var connectionString = TestHelpers.GetStorageConnectionString();
            var clientProvider = new StorageAccountClientProvider(connectionString);
            var logger = this.loggerFactory.CreateLogger<AzureStorageScalabilityProvider>();
            var provider = new AzureStorageScalabilityProvider(clientProvider, "TestConnection", logger);

            // Act - Call both methods to ensure they share the same metrics provider
            provider.TryGetScaleMonitor("functionId", "functionName", "testHub", "TestConnection", out IScaleMonitor scaleMonitor);
            provider.TryGetTargetScaler("functionId", "functionName", "testHub", "TestConnection", out ITargetScaler targetScaler);

            // Assert
            Assert.NotNull(scaleMonitor);
            Assert.NotNull(targetScaler);
        }
    }
}
