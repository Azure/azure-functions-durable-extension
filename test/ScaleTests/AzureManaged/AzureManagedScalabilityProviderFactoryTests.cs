// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.AzureManaged;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Tests
{
    public class AzureManagedScalabilityProviderFactoryTests
    {
        private readonly ITestOutputHelper output;
        private readonly ILoggerFactory loggerFactory;
        private readonly IConfiguration configuration;

        public AzureManagedScalabilityProviderFactoryTests(ITestOutputHelper output)
        {
            this.output = output;
            this.loggerFactory = new LoggerFactory();
            this.loggerFactory.AddProvider(new TestLoggerProvider(output));

            this.configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "v3-dtsConnectionMI", "Endpoint=https://test.westus.durabletask.io;Authentication=DefaultAzure" },
                    { "DURABLE_TASK_SCHEDULER_CONNECTION_STRING", "Endpoint=https://default.westus.durabletask.io;Authentication=DefaultAzure" },
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
            var factory = new AzureManagedScalabilityProviderFactory(
                this.configuration,
                this.loggerFactory);

            Assert.NotNull(factory);
            Assert.Equal("AzureManaged", factory.Name);
            Assert.Equal("DURABLE_TASK_SCHEDULER_CONNECTION_STRING", factory.DefaultConnectionName);
        }

        /// <summary>
        /// KEY SCENARIO: Scale Controller sends trigger metadata with storageProvider.type = "azureManaged".
        /// Validates that the factory returns an AzureManagedScalabilityProvider with the correct
        /// connection name and concurrency limits taken from the trigger metadata.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_WithTriggerMetadata_ReturnsAzureManagedProvider()
        {
            var factory = new AzureManagedScalabilityProviderFactory(
                this.configuration,
                this.loggerFactory);

            var triggerMetadata = TestHelpers.CreateTriggerMetadata("testHub", 15, 25, "v3-dtsConnectionMI", "azureManaged");
            var metadata = triggerMetadata.ExtractDurableTaskMetadata();

            var provider = factory.GetScalabilityProvider(metadata, triggerMetadata);

            Assert.NotNull(provider);
            Assert.IsType<AzureManagedScalabilityProvider>(provider);
            var azureProvider = (AzureManagedScalabilityProvider)provider;

            // The provider connection name should be same as what we set at metadata.
            Assert.Equal("v3-dtsConnectionMI", azureProvider.ConnectionName);
            Assert.Equal(15, azureProvider.MaxConcurrentTaskOrchestrationWorkItems);
            Assert.Equal(25, azureProvider.MaxConcurrentTaskActivityWorkItems);
        }

        /// <summary>
        /// Validates that when trigger metadata does not include a connectionName in storageProvider,
        /// the factory falls back to the default connection name DURABLE_TASK_SCHEDULER_CONNECTION_STRING.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_WithNoConnectionNameInMetadata_UsesDefaultConnectionName()
        {
            var factory = new AzureManagedScalabilityProviderFactory(
                this.configuration,
                this.loggerFactory);

            var jobj = new JObject
            {
                { "functionName", "TestFunction" },
                { "taskHubName", "testHub" },
                { "storageProvider", new JObject { { "type", "azureManaged" } } },
            };
            var triggerMetadata = new TriggerMetadata(jobj);
            var metadata = triggerMetadata.ExtractDurableTaskMetadata();

            var provider = factory.GetScalabilityProvider(metadata, triggerMetadata);

            Assert.NotNull(provider);
            Assert.Equal("DURABLE_TASK_SCHEDULER_CONNECTION_STRING", provider.ConnectionName);
        }

        /// <summary>
        /// Validates that when the specified connection string is absent from configuration,
        /// the factory throws an InvalidOperationException rather than silently continuing.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_MissingConnectionString_ThrowsInvalidOperationException()
        {
            var emptyConfig = new ConfigurationBuilder().Build();
            var factory = new AzureManagedScalabilityProviderFactory(
                emptyConfig,
                this.loggerFactory);

            var triggerMetadata = TestHelpers.CreateTriggerMetadata("testHub", 5, 10, "MISSING_CONNECTION", "azureManaged");
            var metadata = triggerMetadata.ExtractDurableTaskMetadata();

            Assert.Throws<InvalidOperationException>(() => factory.GetScalabilityProvider(metadata, triggerMetadata));
        }

        /// <summary>
        /// Validates that calling GetScalabilityProvider twice with the same connection name and task hub
        /// returns the same cached provider instance, avoiding redundant connections per scale decision.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_SameParameters_ReturnsCachedInstance()
        {
            var factory = new AzureManagedScalabilityProviderFactory(
                this.configuration,
                this.loggerFactory);

            var triggerMetadata = TestHelpers.CreateTriggerMetadata("testHub", 5, 10, "v3-dtsConnectionMI", "azureManaged");
            var metadata = triggerMetadata.ExtractDurableTaskMetadata();

            var provider1 = factory.GetScalabilityProvider(metadata, triggerMetadata);
            var provider2 = factory.GetScalabilityProvider(metadata, triggerMetadata);

            Assert.Same(provider1, provider2);
        }

        /// <summary>
        /// KEY SCENARIO: When multiple provider factories are registered and trigger metadata specifies
        /// storageProvider.type = "azureManaged", DurableTaskScaleExtension.GetScalabilityProviderFactory
        /// must select the AzureManagedScalabilityProviderFactory.
        /// </summary>
        [Fact]
        public void GetScalabilityProviderFactory_WhenMetadataTypeIsAzureManaged_SelectsAzureManagedFactory()
        {
            var azureManagedFactory = new AzureManagedScalabilityProviderFactory(
                this.configuration,
                this.loggerFactory);

            // Create a list of mock Azure Storage provider factories to simulate multiple provider factories.
            IScalabilityProviderFactory[] factories = new IScalabilityProviderFactory[]
            {
                new StubAzureStorageFactory(),
                azureManagedFactory,
            };

            var triggerMetadata = TestHelpers.CreateTriggerMetadata("testHub", 5, 10, "DURABLE_TASK_SCHEDULER_CONNECTION_STRING", "azureManaged");
            var metadata = triggerMetadata.ExtractDurableTaskMetadata();

            var logger = this.loggerFactory.CreateLogger("test");
            var selectedFactory = DurableTaskScaleExtension.GetScalabilityProviderFactory(metadata, logger, factories);

            Assert.IsType<AzureManagedScalabilityProviderFactory>(selectedFactory);
        }

        // Stub used to test factory selection without a real storage emulator.
        private class StubAzureStorageFactory : IScalabilityProviderFactory
        {
            public string Name => "AzureStorage";

            public string DefaultConnectionName => "AzureWebJobsStorage";

            public ScalabilityProvider GetScalabilityProvider(DurableTaskMetadata metadata, TriggerMetadata triggerMetadata)
                => throw new NotImplementedException();
        }
    }
}
