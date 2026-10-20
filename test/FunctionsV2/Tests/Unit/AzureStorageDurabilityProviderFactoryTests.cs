// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace WebJobs.Extensions.DurableTask.Tests.V2
{
    public class AzureStorageDurabilityProviderFactoryTests
    {
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void DefaultWorkerId_IsMachineName()
        {
            var clientProviderFactory = new TestStorageServiceClientProviderFactory();
            var mockOptions = new OptionsWrapper<DurableTaskOptions>(new DurableTaskOptions());
            var nameResolver = new Mock<INameResolver>().Object;
            var factory = new AzureStorageDurabilityProviderFactory(
                mockOptions,
                clientProviderFactory,
                nameResolver,
                NullLoggerFactory.Instance,
                TestHelpers.GetMockPlatformInformationService());

            var settings = factory.GetAzureStorageOrchestrationServiceSettings();

            Assert.Equal(Environment.MachineName, settings.WorkerId);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ConsumptionDefaultsAreApplied()
        {
            var clientProviderFactory = new TestStorageServiceClientProviderFactory();
            var mockOptions = new OptionsWrapper<DurableTaskOptions>(new DurableTaskOptions());
            var nameResolver = new Mock<INameResolver>().Object;
            var factory = new AzureStorageDurabilityProviderFactory(
                mockOptions,
                clientProviderFactory,
                nameResolver,
                NullLoggerFactory.Instance,
                TestHelpers.GetMockPlatformInformationService(inConsumption: true));

            var settings = factory.GetAzureStorageOrchestrationServiceSettings();

            Assert.Equal(128, settings.ControlQueueBufferThreshold);
            Assert.Equal(5, settings.MaxConcurrentTaskOrchestrationWorkItems);
            Assert.Equal(10, settings.MaxConcurrentTaskActivityWorkItems);
            Assert.Equal(25, settings.MaxStorageOperationConcurrency);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ConsumptionDefaultsForPythonAreApplied()
        {
            var clientProviderFactory = new TestStorageServiceClientProviderFactory();
            var mockOptions = new OptionsWrapper<DurableTaskOptions>(new DurableTaskOptions());
            var nameResolver = new Mock<INameResolver>().Object;
            var factory = new AzureStorageDurabilityProviderFactory(
                mockOptions,
                clientProviderFactory,
                nameResolver,
                NullLoggerFactory.Instance,
                TestHelpers.GetMockPlatformInformationService(inConsumption: true, language: WorkerRuntimeType.Python));

            var settings = factory.GetAzureStorageOrchestrationServiceSettings();

            Assert.Equal(32, settings.ControlQueueBufferThreshold);
            Assert.Equal(5, settings.MaxConcurrentTaskOrchestrationWorkItems);
            Assert.Equal(10, settings.MaxConcurrentTaskActivityWorkItems);
            Assert.Equal(25, settings.MaxStorageOperationConcurrency);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(WorkerRuntimeType.Native)]
        [InlineData(WorkerRuntimeType.Golang)]
        public void GrpcRuntimes_UseSeparateQueueForEntityWorkItems(WorkerRuntimeType runtimeType)
        {
            var clientProviderFactory = new TestStorageServiceClientProviderFactory();
            var mockOptions = new OptionsWrapper<DurableTaskOptions>(new DurableTaskOptions());
            var nameResolver = new Mock<INameResolver>().Object;
            var factory = new AzureStorageDurabilityProviderFactory(
                mockOptions,
                clientProviderFactory,
                nameResolver,
                NullLoggerFactory.Instance,
                TestHelpers.GetMockPlatformInformationService(language: runtimeType));

            var settings = factory.GetAzureStorageOrchestrationServiceSettings();

            // The native worker runtimes use the gRPC protocol, which requires a separate
            // queue for entity work items (matching .NET isolated / Java behavior).
            Assert.True(settings.UseSeparateQueueForEntityWorkItems);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void InProcRuntime_DoesNotUseSeparateQueueForEntityWorkItems()
        {
            var clientProviderFactory = new TestStorageServiceClientProviderFactory();
            var mockOptions = new OptionsWrapper<DurableTaskOptions>(new DurableTaskOptions());
            var nameResolver = new Mock<INameResolver>().Object;
            var factory = new AzureStorageDurabilityProviderFactory(
                mockOptions,
                clientProviderFactory,
                nameResolver,
                NullLoggerFactory.Instance,
                TestHelpers.GetMockPlatformInformationService(language: WorkerRuntimeType.DotNet));

            var settings = factory.GetAzureStorageOrchestrationServiceSettings();

            // The in-process .NET runtime keeps the shared queue (this default is flipped
            // to true only for the gRPC-based runtimes).
            Assert.False(settings.UseSeparateQueueForEntityWorkItems);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ConsumptionDefaultsAreNotAlwaysApplied()
        {
            var clientProviderFactory = new TestStorageServiceClientProviderFactory();
            var mockOptions = new OptionsWrapper<DurableTaskOptions>(new DurableTaskOptions());
            var nameResolver = new Mock<INameResolver>().Object;
            var factory = new AzureStorageDurabilityProviderFactory(
                mockOptions,
                clientProviderFactory,
                nameResolver,
                NullLoggerFactory.Instance,
                TestHelpers.GetMockPlatformInformationService(inConsumption: false));

            var settings = factory.GetAzureStorageOrchestrationServiceSettings();

            // We want to make sure that the consumption defaults (listed below)
            // aren't applied on non-consumption plans.
            Assert.NotEqual(32, settings.ControlQueueBufferThreshold);
            Assert.NotEqual(5, settings.MaxConcurrentTaskOrchestrationWorkItems);
            Assert.NotEqual(10, settings.MaxConcurrentTaskActivityWorkItems);
            Assert.NotEqual(25, settings.MaxStorageOperationConcurrency);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ConsumptionDefaultsDoNotOverrideCustomerOptions()
        {
            var storageAccountProvider = new TestStorageServiceClientProviderFactory();
            var options = new DurableTaskOptions();

            options.StorageProvider.Add("ControlQueueBufferThreshold", 999);
            options.MaxConcurrentOrchestratorFunctions = 888;
            options.MaxConcurrentActivityFunctions = 777;

            var mockOptions = new OptionsWrapper<DurableTaskOptions>(options);
            var nameResolver = new Mock<INameResolver>().Object;
            var factory = new AzureStorageDurabilityProviderFactory(
                mockOptions,
                storageAccountProvider,
                nameResolver,
                NullLoggerFactory.Instance,
                TestHelpers.GetMockPlatformInformationService(inConsumption: true));

            var settings = factory.GetAzureStorageOrchestrationServiceSettings();

            // We want to make sure that the consumption defaults (listed below)
            // aren't applied on non-consumption plans.
            Assert.Equal(999, settings.ControlQueueBufferThreshold);
            Assert.Equal(888, settings.MaxConcurrentTaskOrchestrationWorkItems);
            Assert.Equal(777, settings.MaxConcurrentTaskActivityWorkItems);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void LegacyPartitionManagementConfiguredAndTablePartitionManagementNotConfigured_DisablesDefaultTablePartitionManagement()
        {
            var clientProviderFactory = new TestStorageServiceClientProviderFactory();
            DurableTaskOptions options = BindDurableTaskOptions(
                new Dictionary<string, string>
                {
                    { "useLegacyPartitionManagement", "true" },
                });

            Assert.Single(options.StorageProvider);
            Assert.True(options.StorageProvider.ContainsKey("useLegacyPartitionManagement"));
            Assert.False(options.StorageProvider.ContainsKey("useTablePartitionManagement"));

            var loggerProvider = new TestLoggerProvider(null);
            using var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(loggerProvider);
            var factory = new AzureStorageDurabilityProviderFactory(
                new OptionsWrapper<DurableTaskOptions>(options),
                clientProviderFactory,
                new Mock<INameResolver>().Object,
                loggerFactory,
                TestHelpers.GetMockPlatformInformationService());

            var settings = factory.GetAzureStorageOrchestrationServiceSettings();

            Assert.True(settings.UseLegacyPartitionManagement);
            Assert.False(settings.UseTablePartitionManagement);
            var warning = Assert.Single(
                loggerProvider.GetAllLogMessages(),
                message =>
                    message.Level == LogLevel.Warning &&
                    message.FormattedMessage.Contains("Disabling `useTablePartitionManagement` to preserve legacy partition management."));
            Assert.Equal("Host.Triggers.DurableTask.AzureStorage", warning.Category);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ExplicitPartitionManagementConflictIsNotNormalized()
        {
            var clientProviderFactory = new TestStorageServiceClientProviderFactory();
            DurableTaskOptions options = BindDurableTaskOptions(
                new Dictionary<string, string>
                {
                    { "useLegacyPartitionManagement", "true" },
                    { "useTablePartitionManagement", "true" },
                });

            Assert.Equal(2, options.StorageProvider.Count);
            Assert.True(options.StorageProvider.ContainsKey("useLegacyPartitionManagement"));
            Assert.True(options.StorageProvider.ContainsKey("useTablePartitionManagement"));

            var factory = new AzureStorageDurabilityProviderFactory(
                new OptionsWrapper<DurableTaskOptions>(options),
                clientProviderFactory,
                new Mock<INameResolver>().Object,
                NullLoggerFactory.Instance,
                TestHelpers.GetMockPlatformInformationService());

            var settings = factory.GetAzureStorageOrchestrationServiceSettings();

            Assert.True(settings.UseLegacyPartitionManagement);
            Assert.True(settings.UseTablePartitionManagement);
            Assert.Throws<ArgumentException>(() => factory.GetDurabilityProvider());
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void PartitionManagementNotConfigured_KeepsDefaultTablePartitionManagement()
        {
            var clientProviderFactory = new TestStorageServiceClientProviderFactory();
            DurableTaskOptions options = BindDurableTaskOptions(new Dictionary<string, string>());

            Assert.Empty(options.StorageProvider);

            var factory = new AzureStorageDurabilityProviderFactory(
                new OptionsWrapper<DurableTaskOptions>(options),
                clientProviderFactory,
                new Mock<INameResolver>().Object,
                NullLoggerFactory.Instance,
                TestHelpers.GetMockPlatformInformationService());

            var settings = factory.GetAzureStorageOrchestrationServiceSettings();

            Assert.False(settings.UseLegacyPartitionManagement);
            Assert.True(settings.UseTablePartitionManagement);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void EnvironmentIsVMSS_WorkerIdFromEnvironmentVariables()
        {
            var clientProviderFactory = new TestStorageServiceClientProviderFactory();
            var mockOptions = new OptionsWrapper<DurableTaskOptions>(new DurableTaskOptions());
            var nameResolver = new SimpleNameResolver(new Dictionary<string, string>()
            {
                { "WEBSITE_CURRENT_STAMPNAME", "waws-prod-euapbn1-003" },
                { "RoleInstanceId", "dw0SmallDedicatedWebWorkerRole_hr0HostRole-3-VM-13" },
            });

            var factory = new AzureStorageDurabilityProviderFactory(
                mockOptions,
                clientProviderFactory,
                nameResolver,
                NullLoggerFactory.Instance,
                TestHelpers.GetMockPlatformInformationService());

            var settings = factory.GetAzureStorageOrchestrationServiceSettings();

            Assert.Equal("waws-prod-euapbn1-003:dw0SmallDedicatedWebWorkerRole_hr0HostRole-3-VM-13", settings.WorkerId);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CustomConnectionNameIsResolved()
        {
            var storageAccountProvider = new CustomTestStorageAccountProvider("CustomConnection");
            var mockOptions = new OptionsWrapper<DurableTaskOptions>(new DurableTaskOptions());
            var nameResolver = new Mock<INameResolver>().Object;

            var factory = new AzureStorageDurabilityProviderFactory(
                mockOptions,
                storageAccountProvider,
                nameResolver,
                NullLoggerFactory.Instance,
                TestHelpers.GetMockPlatformInformationService());

            factory.GetDurabilityProvider(); // This will initialize the default connection string
            var provider = factory.GetDurabilityProvider(new DurableClientAttribute() { ConnectionName = "CustomConnection", TaskHub = "TestHubName" });

            Assert.Equal("CustomConnection", provider.ConnectionName);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void DefaultConnectionNameIsResolved()
        {
            var storageAccountProvider = new CustomTestStorageAccountProvider("CustomConnection");
            var mockOptions = new OptionsWrapper<DurableTaskOptions>(new DurableTaskOptions());
            var nameResolver = new Mock<INameResolver>().Object;

            var factory = new AzureStorageDurabilityProviderFactory(
                mockOptions,
                storageAccountProvider,
                nameResolver,
                NullLoggerFactory.Instance,
                TestHelpers.GetMockPlatformInformationService());

            var provider = factory.GetDurabilityProvider();

            Assert.Equal("Storage", provider.ConnectionName);
        }

        // Tests that an unset hub name derived from a site name over 45 characters is truncated
        // and logs a warning about possible collisions.
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void DefaultHubNameDerivedFromLongSiteName_LogsCollisionWarning()
        {
            string originalSiteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
            string siteName = new string('a', 47);
            string expectedHubName = new string('a', 45);

            try
            {
                Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", siteName);

                // Leave HubName unset so DurableTaskOptions derives its default from WEBSITE_SITE_NAME.
                var options = new DurableTaskOptions();
                var loggerProvider = new TestLoggerProvider(null);
                using var loggerFactory = new LoggerFactory();
                loggerFactory.AddProvider(loggerProvider);
                var factory = new AzureStorageDurabilityProviderFactory(
                    new OptionsWrapper<DurableTaskOptions>(options),
                    new TestStorageServiceClientProviderFactory(),
                    new Mock<INameResolver>().Object,
                    loggerFactory,
                    TestHelpers.GetMockPlatformInformationService());

                factory.GetDurabilityProvider();

                Assert.Equal(expectedHubName, options.HubName);
                var warning = Assert.Single(
                    loggerProvider.GetAllLogMessages(),
                    message =>
                        message.Level == LogLevel.Warning &&
                        message.FormattedMessage.Contains("The default task hub name"));
                Assert.Contains("was truncated", warning.FormattedMessage);
                Assert.Contains("task hub collisions", warning.FormattedMessage);
                Assert.Contains(
                    "https://go.microsoft.com/fwlink/?LinkId=2377701",
                    warning.FormattedMessage);
            }
            finally
            {
                Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", originalSiteName);
            }
        }

        // Tests that an unset hub name derived from a 45-character site name remains unchanged
        // and does not log a collision warning.
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void DefaultHubNameDerivedFromSiteNameAtLimit_DoesNotLogCollisionWarning()
        {
            string originalSiteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
            string siteName = new string('a', 45);

            try
            {
                Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", siteName);

                // Leave HubName unset so DurableTaskOptions derives its default from WEBSITE_SITE_NAME.
                var options = new DurableTaskOptions();
                var loggerProvider = new TestLoggerProvider(null);
                using var loggerFactory = new LoggerFactory();
                loggerFactory.AddProvider(loggerProvider);
                var factory = new AzureStorageDurabilityProviderFactory(
                    new OptionsWrapper<DurableTaskOptions>(options),
                    new TestStorageServiceClientProviderFactory(),
                    new Mock<INameResolver>().Object,
                    loggerFactory,
                    TestHelpers.GetMockPlatformInformationService());

                factory.GetDurabilityProvider();

                // A site name at the limit remains unchanged and does not produce a warning.
                Assert.Equal(siteName, options.HubName);
                Assert.DoesNotContain(
                    loggerProvider.GetAllLogMessages(),
                    message =>
                        message.Level == LogLevel.Warning &&
                        message.FormattedMessage.Contains("task hub collisions"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", originalSiteName);
            }
        }

        private static DurableTaskOptions BindDurableTaskOptions(
            IDictionary<string, string> storageProviderSettings)
        {
            var configuration = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> setting in storageProviderSettings)
            {
                configuration.Add(
                    $"AzureWebJobs:extensions:DurableTask:storageProvider:{setting.Key}",
                    setting.Value);
            }

            using IHost host = new HostBuilder()
                .ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(configuration))
                .ConfigureWebJobs(builder => builder.AddDurableTask())
                .Build();

            return host.Services.GetRequiredService<IOptions<DurableTaskOptions>>().Value;
        }
    }
}
