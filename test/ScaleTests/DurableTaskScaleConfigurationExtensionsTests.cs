// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.AzureManaged;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.AzureStorage;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Netherite;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Tests
{
    /// <summary>
    /// Tests for DurableTaskScaleConfigurationExtensions.
    /// Validates Dependency Injection registration when Scale Controller calls AddDurableTask().
    /// Ensures all required services are properly registered for the scale package to function.
    /// </summary>
    public class DurableTaskScaleConfigurationExtensionsTests
    {
        /// <summary>
        /// Validates that AddDurableTask() registers IStorageServiceClientProviderFactory.
        /// Validates that AddDurableTask() registers IScalabilityProviderFactory implementations.
        /// Ensures Scale Controller can resolve storage clients and scalability providers.
        /// </summary>
        [Fact]
        public void AddDurableTask_RegistersRequiredServices()
        {
            // Arrange
            // Use TestWebJobsBuilder directly (no HostBuilder needed) - this matches how Scale Controller uses it
            using var loggerFactory = new LoggerFactory();
            var services = new ServiceCollection();
            services.AddSingleton<Microsoft.Azure.WebJobs.INameResolver>(new SimpleNameResolver());
            services.AddSingleton<ILoggerFactory>(loggerFactory);
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

            var webJobsBuilder = new TestWebJobsBuilder(services);

            webJobsBuilder.AddDurableTask();

            // Build service provider to resolve services
            using (var serviceProvider = services.BuildServiceProvider())
            {
                // Verify IStorageServiceClientProviderFactory is registered
                var clientProviderFactory = serviceProvider.GetService<IStorageServiceClientProviderFactory>();
                Assert.NotNull(clientProviderFactory);

                // Verify IScalabilityProviderFactory is registered
                var scalabilityProviderFactories = serviceProvider.GetServices<IScalabilityProviderFactory>().ToList();
                Assert.NotEmpty(scalabilityProviderFactories);
                Assert.Contains(scalabilityProviderFactories, f => f is AzureStorageScalabilityProviderFactory);
                Assert.Contains(scalabilityProviderFactories, f => f is AzureManagedScalabilityProviderFactory);
                Assert.Contains(scalabilityProviderFactories, f => f is NetheriteScalabilityProviderFactory);
            }
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
            // Use TestWebJobsBuilder directly (no HostBuilder needed) - this matches how Scale Controller uses it
            using var loggerFactory = new LoggerFactory();
            var services = new ServiceCollection();
            services.AddSingleton<INameResolver>(new SimpleNameResolver());
            services.AddSingleton<ILoggerFactory>(loggerFactory);
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

            var webJobsBuilder = new TestWebJobsBuilder(services);

            webJobsBuilder.AddDurableTask();

            // Verify DurableTaskScaleExtension is registered by checking service descriptors
            var extensionDescriptor = services
                .FirstOrDefault(d => d.ServiceType == typeof(IExtensionConfigProvider)
                                  && d.ImplementationType == typeof(DurableTaskScaleExtension));
            Assert.NotNull(extensionDescriptor);
        }

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
            // Use TestWebJobsBuilder directly (no HostBuilder needed) - this matches how Scale Controller uses it
            using var loggerFactory = new LoggerFactory();
            var services = new ServiceCollection();
            services.AddSingleton<INameResolver>(new SimpleNameResolver());
            services.AddSingleton<ILoggerFactory>(loggerFactory);
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

            var webJobsBuilder = new TestWebJobsBuilder(services);

            webJobsBuilder.AddDurableTask();

            // Build service provider to resolve services
            using (var serviceProvider = services.BuildServiceProvider())
            {
                // Verify the same instance is returned (singleton)
                var factory1 = serviceProvider.GetService<IStorageServiceClientProviderFactory>();
                var factory2 = serviceProvider.GetService<IStorageServiceClientProviderFactory>();
                Assert.Same(factory1, factory2);
            }
        }

        /// <summary>
        /// Validates that AddDurableTask() registers AzureStorageScalabilityProviderFactory.
        /// Tests that Azure Storage is configured as the default scalability provider.
        /// Verifies factory name is "AzureStorage" for Scale Controller identification.
        /// Ensures backward compatibility with existing Azure Functions deployments.
        /// </summary>
        [Fact]
        public void AddDurableTask_RegistersAzureStorageAsDefaultProvider()
        {
            // Arrange
            // Use TestWebJobsBuilder directly (no HostBuilder needed) - this matches how Scale Controller uses it
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "AzureWebJobsStorage", "UseDevelopmentStorage=true" },
                })
                .Build();

            using var loggerFactory = new LoggerFactory();
            var services = new ServiceCollection();
            services.AddSingleton<INameResolver>(new SimpleNameResolver());
            services.AddSingleton<ILoggerFactory>(loggerFactory);
            services.AddSingleton<IConfiguration>(configuration);

            var webJobsBuilder = new TestWebJobsBuilder(services);

            webJobsBuilder.AddDurableTask();

            using (var serviceProvider = services.BuildServiceProvider())
            {
                // Verify AzureStorageScalabilityProviderFactory is registered as the default
                var scalabilityProviderFactories = serviceProvider.GetServices<IScalabilityProviderFactory>().ToList();
                Assert.NotEmpty(scalabilityProviderFactories);

                var azureStorageFactory = scalabilityProviderFactories.OfType<AzureStorageScalabilityProviderFactory>().FirstOrDefault();
                Assert.NotNull(azureStorageFactory);
                Assert.Equal("AzureStorage", azureStorageFactory.Name);
            }
        }

        /// <summary>
        /// Validates that factory can resolve multiple connection strings from configuration.
        /// Tests multi-tenant scenarios where different functions use different storage accounts.
        /// Verifies end-to-end DI setup with IConfiguration integration.
        /// Ensures Scale Controller can handle functions with different connection configurations.
        /// </summary>
        [Fact]
        public void AddDurableTask_WithMultipleConnections_AllCanBeResolved()
        {
            // Arrange - Set up configuration with multiple connections
            // Use TestWebJobsBuilder directly (no HostBuilder needed) - this matches how Scale Controller uses it
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "AzureWebJobsStorage", "UseDevelopmentStorage=true" },
                    { "Connection1", "UseDevelopmentStorage=true" },
                    { "Connection2", "UseDevelopmentStorage=true" },
                })
                .Build();

            using var loggerFactory = new LoggerFactory();
            var services = new ServiceCollection();
            services.AddSingleton<INameResolver>(new SimpleNameResolver());
            services.AddSingleton<ILoggerFactory>(loggerFactory);
            services.AddSingleton<IConfiguration>(configuration);

            var webJobsBuilder = new TestWebJobsBuilder(services);
            webJobsBuilder.AddDurableTask();

            // Build service provider to resolve services
            using (var serviceProvider = services.BuildServiceProvider())
            {
                // Verify we can create client providers for different connections
                var clientProviderFactory = serviceProvider.GetService<IStorageServiceClientProviderFactory>();
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
    }
}
