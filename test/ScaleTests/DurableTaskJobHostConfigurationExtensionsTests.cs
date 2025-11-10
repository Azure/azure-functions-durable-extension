// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.AzureManaged;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.AzureStorage;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Sql;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Tests
{
    /// <summary>
    /// Tests for DurableTaskJobHostConfigurationExtensions.
    /// Validates Dependency Injection registration when Scale Controller calls AddDurableTask().
    /// Ensures all required services are properly registered for the scale package to function.
    /// </summary>
    public class DurableTaskJobHostConfigurationExtensionsTests
    {
        /// <summary>
        /// Scenario: Core service registration in DI container.
        /// Validates that AddDurableTask() registers IStorageServiceClientProviderFactory.
        /// Validates that AddDurableTask() registers IScalabilityProviderFactory implementations.
        /// Tests the foundational setup required by Scale Controller integration.
        /// Ensures Scale Controller can resolve storage clients and scalability providers.
        /// </summary>
        [Fact]
        public void AddDurableTask_RegistersRequiredServices()
        {
            // Arrange
            var hostBuilder = new HostBuilder();
            hostBuilder.ConfigureServices(services =>
            {
                // Register INameResolver - required by AzureStorageScalabilityProviderFactory
                services.AddSingleton<INameResolver>(new SimpleNameResolver());
            });
            hostBuilder.ConfigureWebJobs(webJobsBuilder =>
            {
                // Act
                webJobsBuilder.AddDurableTask();
            });

            var host = hostBuilder.Build();
            var services = host.Services;

            // Assert
            // Verify IStorageServiceClientProviderFactory is registered
            var clientProviderFactory = services.GetService<IStorageServiceClientProviderFactory>();
            Assert.NotNull(clientProviderFactory);

            // Verify IScalabilityProviderFactory is registered
            var scalabilityProviderFactories = services.GetServices<IScalabilityProviderFactory>().ToList();
            Assert.NotEmpty(scalabilityProviderFactories);
            Assert.Contains(scalabilityProviderFactories, f => f is AzureStorageScalabilityProviderFactory);
            Assert.Contains(scalabilityProviderFactories, f => f is AzureManagedScalabilityProviderFactory);
        }

        /// <summary>
        /// Scenario: DurableTaskScaleExtension registration.
        /// Validates that the core extension config provider is registered.
        /// Tests that Scale Controller can initialize the extension.
        /// Ensures WebJobs framework can discover and configure the scale extension.
        /// </summary>
        [Fact]
        public void AddDurableTask_RegistersDurableTaskScaleExtension()
        {
            // Arrange
            var hostBuilder = new HostBuilder();
            hostBuilder.ConfigureServices(services =>
            {
                // Register INameResolver - required by AzureStorageScalabilityProviderFactory
                services.AddSingleton<INameResolver>(new SimpleNameResolver());
            });
            
            IServiceCollection capturedServices = null;
            hostBuilder.ConfigureWebJobs(webJobsBuilder =>
            {
                // Capture the service collection before building
                capturedServices = webJobsBuilder.Services;
                
                // Act
                webJobsBuilder.AddDurableTask();
            });

            var host = hostBuilder.Build();

            // Assert
            // Verify DurableTaskScaleExtension is registered by checking service descriptors
            Assert.NotNull(capturedServices);
            var extensionDescriptor = capturedServices
                .FirstOrDefault(d => d.ServiceType == typeof(Microsoft.Azure.WebJobs.Host.Config.IExtensionConfigProvider) 
                                  && d.ImplementationType == typeof(DurableTaskScaleExtension));
            Assert.NotNull(extensionDescriptor);
        }

        // Test removed: DurableTaskScaleOptions no longer exists - we now rely solely on TriggerMetadata from Scale Controller

        /// <summary>
        /// Scenario: Singleton registration for storage client factory.
        /// Validates that IStorageServiceClientProviderFactory is registered as singleton.
        /// Tests that multiple resolutions return the same instance (connection pooling).
        /// Ensures efficient resource usage and connection reuse across scale operations.
        /// </summary>
        [Fact]
        public void AddDurableTask_RegistersSingletonClientProviderFactory()
        {
            // Arrange
            var hostBuilder = new HostBuilder();
            hostBuilder.ConfigureWebJobs(webJobsBuilder =>
            {
                // Act
                webJobsBuilder.AddDurableTask();
            });

            var host = hostBuilder.Build();
            var services = host.Services;

            // Assert
            // Verify the same instance is returned (singleton)
            var factory1 = services.GetService<IStorageServiceClientProviderFactory>();
            var factory2 = services.GetService<IStorageServiceClientProviderFactory>();
            Assert.Same(factory1, factory2);
        }

        /// <summary>
        /// Scenario: Extension method validation.
        /// Validates that AddDurableTask() properly handles null builder parameter.
        /// Tests defensive programming and error handling.
        /// Ensures clear error messages for misconfiguration scenarios.
        /// </summary>
        [Fact]
        public void AddDurableTask_NullBuilder_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<System.ArgumentNullException>(() =>
            {
                IWebJobsBuilder builder = null;
                builder.AddDurableTask();
            });
        }

        /// <summary>
        /// ✅ KEY SCENARIO 1: Default Azure Storage provider registration.
        /// Validates that AddDurableTask() registers AzureStorageScalabilityProviderFactory.
        /// Tests that Azure Storage is configured as the default scalability provider.
        /// Verifies factory name is "AzureStorage" for Scale Controller identification.
        /// Ensures backward compatibility with existing Azure Functions deployments.
        /// </summary>
        [Fact]
        public void AddDurableTask_RegistersAzureStorageAsDefaultProvider()
        {
            // Arrange
            var hostBuilder = new HostBuilder();
            hostBuilder.ConfigureServices(services =>
            {
                services.AddSingleton<INameResolver>(new SimpleNameResolver());
            });
            hostBuilder.ConfigureWebJobs(webJobsBuilder =>
            {
                // Act
                webJobsBuilder.AddDurableTask();
            });

            var host = hostBuilder.Build();
            var services = host.Services;

            // Assert
            // Verify AzureStorageScalabilityProviderFactory is registered as the default
            var scalabilityProviderFactories = services.GetServices<IScalabilityProviderFactory>().ToList();
            Assert.NotEmpty(scalabilityProviderFactories);
            
            var azureStorageFactory = scalabilityProviderFactories.OfType<AzureStorageScalabilityProviderFactory>().FirstOrDefault();
            Assert.NotNull(azureStorageFactory);
            Assert.Equal("AzureStorage", azureStorageFactory.Name);
        }

        /// <summary>
        /// ✅ KEY SCENARIO 2: Multiple connections configuration resolution.
        /// Validates that factory can resolve multiple connection strings from configuration.
        /// Tests multi-tenant scenarios where different functions use different storage accounts.
        /// Verifies end-to-end DI setup with IConfiguration integration.
        /// Ensures Scale Controller can handle functions with different connection configurations.
        /// </summary>
        [Fact]
        public void AddDurableTask_WithMultipleConnections_AllCanBeResolved()
        {
            // Arrange - Set up configuration with multiple connections
            var hostBuilder = new HostBuilder();
            hostBuilder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "AzureWebJobsStorage", "UseDevelopmentStorage=true" },
                    { "Connection1", "UseDevelopmentStorage=true" },
                    { "Connection2", "UseDevelopmentStorage=true" },
                });
            });
            hostBuilder.ConfigureServices(services =>
            {
                services.AddSingleton<INameResolver>(new SimpleNameResolver());
            });
            hostBuilder.ConfigureWebJobs(webJobsBuilder =>
            {
                webJobsBuilder.AddDurableTask();
            });

            var host = hostBuilder.Build();
            var services = host.Services;

            // Assert
            // Verify we can create client providers for different connections
            var clientProviderFactory = services.GetService<IStorageServiceClientProviderFactory>();
            Assert.NotNull(clientProviderFactory);

            // Test that we can get client providers for all connections
            var connections = new[] { "AzureWebJobsStorage", "Connection1", "Connection2" };
            foreach (var connectionName in connections)
            {
                var clientProvider = clientProviderFactory.GetClientProvider(connectionName, null);
                Assert.NotNull(clientProvider);
            }
        }
    }

    /// <summary>
    /// Simple INameResolver implementation for tests that returns the input as-is.
    /// </summary>
    internal class SimpleNameResolver : INameResolver
    {
        public string Resolve(string name)
        {
            return name;
        }
    }

    /// <summary>
    /// Tests for end-to-end SQL Server scaling integration via DurableTaskTriggersScaleProvider.
    /// Validates the complete flow from triggerMetadata to working TargetScaler and ScaleMonitor.
    /// </summary>
    public class DurableTaskTriggersScaleProviderSqlServerTests
    {
        /// <summary>
        /// Scenario: End-to-end SQL Server scaling via triggerMetadata with type="mssql".
        /// Validates that when triggerMetadata mentions storageProvider.type="mssql", DurableTaskTriggersScaleProvider creates SQL provider.
        /// Tests that connection string is retrieved from triggerMetadata.storageProvider.connectionName.
        /// Verifies that both TargetScaler and ScaleMonitor successfully work with real SQL Server.
        /// This test validates the complete integration path that Scale Controller uses.
        /// </summary>
        [Fact]
        public async Task TriggerMetadataWithMssqlType_CreatesSqlProviderViaTriggersScaleProvider_AndBothScalersWork()
        {
            // Arrange - Create triggerMetadata with type="mssql" (as Scale Controller would pass)
            var hubName = "testHub";
            var connectionName = "TestConnection";
            var metadata = new JObject
            {
                { "functionName", "TestFunction" },
                { "type", "activityTrigger" },
                { "taskHubName", hubName },
                { "maxConcurrentOrchestratorFunctions", 10 },
                { "maxConcurrentActivityFunctions", 20 },
                {
                    "storageProvider", new JObject
                    {
                        { "type", "mssql" },
                        { "connectionName", connectionName },
                    }
                },
            };
            var triggerMetadata = new TriggerMetadata(metadata);

            // Verify triggerMetadata has correct storageProvider.type
            var storageProvider = triggerMetadata.Metadata["storageProvider"] as JObject;
            Assert.NotNull(storageProvider);
            Assert.Equal("mssql", storageProvider["type"]?.ToString());
            Assert.Equal(connectionName, storageProvider["connectionName"]?.ToString());

            // Set up DI container with SQL connection string
            var hostBuilder = new HostBuilder();
            hostBuilder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    { $"ConnectionStrings:{connectionName}", TestHelpers.GetSqlConnectionString() },
                    { connectionName, TestHelpers.GetSqlConnectionString() },
                });
            });
            hostBuilder.ConfigureServices(services =>
            {
                services.AddSingleton<INameResolver>(new SimpleNameResolver());
            });
            hostBuilder.ConfigureWebJobs(webJobsBuilder =>
            {
                webJobsBuilder.AddDurableTask();
            });

            var host = hostBuilder.Build();
            var services = host.Services;

            // Get configuration and register SQL factory (as Scale Controller would)
            var configuration = services.GetRequiredService<IConfiguration>();
            var nameResolver = services.GetRequiredService<INameResolver>();
            var loggerFactory = services.GetRequiredService<ILoggerFactory>();
            
            // Register SQL Server factory (normally done by Scale Controller)
            var sqlFactory = new SqlServerScalabilityProviderFactory(
                configuration,
                nameResolver,
                loggerFactory);
            
            // Create a list with all factories (Azure Storage from AddDurableTask + SQL from Scale Controller)
            var scalabilityProviderFactories = new List<IScalabilityProviderFactory>(
                services.GetServices<IScalabilityProviderFactory>());
            scalabilityProviderFactories.Add(sqlFactory);
            
            // Verify SQL Server factory is available
            var sqlFactoryFound = scalabilityProviderFactories.FirstOrDefault(f => f.Name == "mssql");
            Assert.NotNull(sqlFactoryFound);
            Assert.IsType<SqlServerScalabilityProviderFactory>(sqlFactoryFound);

            // Create DurableTaskTriggersScaleProvider (this is what Scale Controller does)
            var triggersScaleProvider = new DurableTaskTriggersScaleProvider(
                nameResolver,
                loggerFactory,
                scalabilityProviderFactories,
                triggerMetadata);

            // Act - Get TargetScaler from DurableTaskTriggersScaleProvider
            var targetScaler = triggersScaleProvider.GetTargetScaler();

            // Assert - TargetScaler was created successfully
            Assert.NotNull(targetScaler);
            Assert.IsType<SqlServerTargetScaler>(targetScaler);

            // Act - Get scale result from TargetScaler (connects to real SQL)
            var targetScalerResult = await targetScaler.GetScaleResultAsync(new TargetScalerContext());

            // Assert - TargetScaler returns valid result
            Assert.NotNull(targetScalerResult);
            Assert.True(targetScalerResult.TargetWorkerCount >= 0, "Target worker count should be non-negative");

            // Act - Get ScaleMonitor from DurableTaskTriggersScaleProvider
            var scaleMonitor = triggersScaleProvider.GetMonitor();

            // Assert - ScaleMonitor was created successfully
            Assert.NotNull(scaleMonitor);
            Assert.IsType<SqlServerScaleMonitor>(scaleMonitor);

            // Act - Get metrics from ScaleMonitor (connects to real SQL)
            var metrics = await scaleMonitor.GetMetricsAsync();

            // Assert - ScaleMonitor returns valid metrics
            Assert.NotNull(metrics);
            Assert.IsType<SqlServerScaleMetric>(metrics);
            var sqlMetrics = (SqlServerScaleMetric)metrics;
            Assert.True(sqlMetrics.RecommendedReplicaCount >= 0, "Recommended replica count should be non-negative");

            // Verify connection string was successfully retrieved
            var connectionString = configuration.GetConnectionString(connectionName) ?? configuration[connectionName];
            Assert.NotNull(connectionString);
            Assert.NotEmpty(connectionString);
        }
    }
}

