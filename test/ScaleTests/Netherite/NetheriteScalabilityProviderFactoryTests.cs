// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using DurableTask.Netherite;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Netherite;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Tests
{
    public class NetheriteScalabilityProviderFactoryTests
    {
        private readonly ITestOutputHelper output;
        private readonly ILoggerFactory loggerFactory;
        private readonly IConfiguration configuration;
        private readonly string storageConnectionString;
        private readonly string eventHubsConnectionString;

        public NetheriteScalabilityProviderFactoryTests(ITestOutputHelper output)
        {
            this.output = output;
            this.loggerFactory = new LoggerFactory();
            this.loggerFactory.AddProvider(new TestLoggerProvider(output));

            this.storageConnectionString = TestHelpers.GetStorageConnectionString();
            this.eventHubsConnectionString = TestHelpers.GetNetheriteEventHubsConnectionString();

            // Default connection name is "Storage,EventHubsConnection".
            this.configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "AzureWebJobsStorage", this.storageConnectionString },
                    { "EventHubsConnection", this.eventHubsConnectionString },
                })
                .Build();
        }

        /// <summary>
        /// Validates that the factory can be instantiated with valid parameters,
        /// reports the correct provider name, and exposes the expected default connection name.
        /// </summary>
        [Fact]
        public void Constructor_ValidParameters_CreatesInstance()
        {
            var factory = new NetheriteScalabilityProviderFactory(
                this.configuration,
                this.loggerFactory);

            Assert.NotNull(factory);
            Assert.Equal("Netherite", factory.Name);
            Assert.Equal("Storage,EventHubsConnection", factory.DefaultConnectionName);
        }

        [Fact]
        public void Constructor_NullConfiguration_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new NetheriteScalabilityProviderFactory(null, this.loggerFactory));
        }

        [Fact]
        public void Constructor_NullLoggerFactory_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new NetheriteScalabilityProviderFactory(this.configuration, null));
        }

        /// <summary>
        /// Scale Controller sends trigger metadata with storageProvider.type = "Netherite" and
        /// a comma-separated connection name. Validates that the factory creates a provider with
        /// the correct connection name and concurrency limits from metadata.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_WithTriggerMetadata_ReturnsNetheriteProvider()
        {
            var factory = new NetheriteScalabilityProviderFactory(
                this.configuration,
                this.loggerFactory);

            var triggerMetadata = TestHelpers.CreateTriggerMetadata(
                "testHub", 15, 25, "AzureWebJobsStorage,EventHubsConnection", "Netherite");
            var metadata = triggerMetadata.ExtractDurableTaskMetadata();

            var provider = factory.GetScalabilityProvider(metadata, triggerMetadata);

            Assert.NotNull(provider);
            Assert.IsType<NetheriteScalabilityProvider>(provider);
            Assert.Equal("AzureWebJobsStorage,EventHubsConnection", provider.ConnectionName);
            Assert.Equal(15, provider.MaxConcurrentTaskOrchestrationWorkItems);
            Assert.Equal(25, provider.MaxConcurrentTaskActivityWorkItems);
        }

        /// <summary>
        /// When no connection name is specified in metadata, the factory uses the default
        /// "Storage,EventHubsConnection" which resolves via AzureWebJobs convention.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_WithNoConnectionNameInMetadata_UsesDefaultConnectionName()
        {
            var factory = new NetheriteScalabilityProviderFactory(
                this.configuration,
                this.loggerFactory);

            var jobj = new JObject
            {
                { "functionName", "TestFunction" },
                { "taskHubName", "testHub" },
                { "storageProvider", new JObject { { "type", "Netherite" } } },
            };
            var triggerMetadata = new TriggerMetadata(jobj);
            var metadata = triggerMetadata.ExtractDurableTaskMetadata();

            var provider = factory.GetScalabilityProvider(metadata, triggerMetadata);

            Assert.NotNull(provider);
            Assert.Equal("Storage,EventHubsConnection", provider.ConnectionName);
        }

        /// <summary>
        /// Validates that a single (non-comma) connection name is used for both storage
        /// and Event Hubs lookups. The storage connection string resolves for storage, but
        /// <see cref="NetheriteOrchestrationServiceSettings.Validate"/> cannot build an Event
        /// Hubs connection from the same value. Netherite wraps that failure in
        /// <see cref="NetheriteConfigurationException"/>.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_WithSingleConnectionName_ThrowsOnValidation()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "AzureWebJobsMyConnection", this.storageConnectionString },
                })
                .Build();

            var factory = new NetheriteScalabilityProviderFactory(config, this.loggerFactory);

            var triggerMetadata = TestHelpers.CreateTriggerMetadata("testHub", 10, 10, "MyConnection", "Netherite");
            var metadata = triggerMetadata.ExtractDurableTaskMetadata();

            Assert.ThrowsAny<NetheriteConfigurationException>(() =>
                factory.GetScalabilityProvider(metadata, triggerMetadata));
        }

        /// <summary>
        /// A comma-separated connection name with more than two parts is invalid and should
        /// produce an InvalidOperationException.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_InvalidCommaFormat_ThrowsInvalidOperationException()
        {
            var factory = new NetheriteScalabilityProviderFactory(
                this.configuration,
                this.loggerFactory);

            var triggerMetadata = TestHelpers.CreateTriggerMetadata("testHub", 10, 10, "A,B,C", "Netherite");
            var metadata = triggerMetadata.ExtractDurableTaskMetadata();

            Assert.Throws<InvalidOperationException>(() =>
                factory.GetScalabilityProvider(metadata, triggerMetadata));
        }

        /// <summary>
        /// Calling GetScalabilityProvider twice with the same connection and task hub
        /// returns the same cached instance.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_SameParameters_ReturnsCachedInstance()
        {
            var factory = new NetheriteScalabilityProviderFactory(
                this.configuration,
                this.loggerFactory);

            var triggerMetadata = TestHelpers.CreateTriggerMetadata(
                "testHub", 10, 10, "AzureWebJobsStorage,EventHubsConnection", "Netherite");
            var metadata = triggerMetadata.ExtractDurableTaskMetadata();

            var provider1 = factory.GetScalabilityProvider(metadata, triggerMetadata);
            var provider2 = factory.GetScalabilityProvider(metadata, triggerMetadata);

            Assert.Same(provider1, provider2);
        }

        /// <summary>
        /// When concurrency settings change between calls, the cached provider is evicted
        /// and a new one is created with the updated limits.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_ConcurrencyChanged_EvictsCacheAndReturnsNewProvider()
        {
            var factory = new NetheriteScalabilityProviderFactory(
                this.configuration,
                this.loggerFactory);

            var triggerMetadata1 = TestHelpers.CreateTriggerMetadata(
                "testHub", 10, 10, "AzureWebJobsStorage,EventHubsConnection", "Netherite");
            var metadata1 = triggerMetadata1.ExtractDurableTaskMetadata();
            var provider1 = factory.GetScalabilityProvider(metadata1, triggerMetadata1);

            Assert.Equal(10, provider1.MaxConcurrentTaskOrchestrationWorkItems);
            Assert.Equal(10, provider1.MaxConcurrentTaskActivityWorkItems);

            // Change concurrency settings
            var triggerMetadata2 = TestHelpers.CreateTriggerMetadata(
                "testHub", 20, 30, "AzureWebJobsStorage,EventHubsConnection", "Netherite");
            var metadata2 = triggerMetadata2.ExtractDurableTaskMetadata();
            var provider2 = factory.GetScalabilityProvider(metadata2, triggerMetadata2);

            Assert.NotSame(provider1, provider2);
            Assert.Equal(20, provider2.MaxConcurrentTaskOrchestrationWorkItems);
            Assert.Equal(30, provider2.MaxConcurrentTaskActivityWorkItems);
        }

        /// <summary>
        /// Validates that when multiple provider factories are registered and trigger metadata
        /// specifies storageProvider.type = "Netherite", the Netherite factory is selected.
        /// </summary>
        [Fact]
        public void GetScalabilityProviderFactory_WhenMetadataTypeIsNetherite_SelectsNetheriteFactory()
        {
            var netheriteFactory = new NetheriteScalabilityProviderFactory(
                this.configuration,
                this.loggerFactory);

            var storageClientProviderFactory = new StorageServiceClientProviderFactory(this.configuration, this.loggerFactory);
            IScalabilityProviderFactory[] factories = new IScalabilityProviderFactory[]
            {
                new AzureStorage.AzureStorageScalabilityProviderFactory(storageClientProviderFactory, this.loggerFactory),
                new AzureManaged.AzureManagedScalabilityProviderFactory(this.configuration, this.loggerFactory),
                netheriteFactory,
            };

            var triggerMetadata = TestHelpers.CreateTriggerMetadata(
                "testHub", 10, 10, "AzureWebJobsStorage,EventHubsConnection", "Netherite");
            var metadata = triggerMetadata.ExtractDurableTaskMetadata();

            var logger = this.loggerFactory.CreateLogger("test");
            var selectedFactory = DurableTaskScaleExtension.GetScalabilityProviderFactory(metadata, logger, factories);

            Assert.IsType<NetheriteScalabilityProviderFactory>(selectedFactory);
        }

        /// <summary>
        /// Identity-based connection: configuration has sub-keys (accountName,
        /// fullyQualifiedNamespace) instead of plain connection strings. Without a token
        /// credential in TriggerMetadata, the resolver cannot build ConnectionInfo from
        /// sub-keys alone, so Validate will fail or produce a provider with null connections.
        /// This test verifies the factory exercises the identity config path.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_WithIdentityConfig_WithoutCredential_Throws()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "AzureWebJobsStorage:accountName", "testaccount" },
                    { "EventHubsConnection:fullyQualifiedNamespace", "testns.servicebus.windows.net" },
                })
                .Build();

            var factory = new NetheriteScalabilityProviderFactory(config, this.loggerFactory);

            var triggerMetadata = TestHelpers.CreateTriggerMetadata(
                "testHub", 10, 10, "AzureWebJobsStorage,EventHubsConnection", "Netherite");
            var metadata = triggerMetadata.ExtractDurableTaskMetadata();

            // Without a TokenCredential, identity-based resolution returns null ConnectionInfo,
            // which will cause Validate or subsequent usage to fail.
            Assert.ThrowsAny<Exception>(() =>
                factory.GetScalabilityProvider(metadata, triggerMetadata));
        }
    }
}
